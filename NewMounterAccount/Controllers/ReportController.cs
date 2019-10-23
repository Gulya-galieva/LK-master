using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DbManager;
using NewMounterAccount.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using NewMounterAccount.AppCode;
using Newtonsoft.Json;
//using MounterAccount.App_code;


namespace NewMounterAccount.Controllers
{
    public class ReportController : Controller
    {
        StoreContext db;
        readonly IHostingEnvironment _env;
        public ReportController(StoreContext context, IHostingEnvironment env)
        {
            db = context;
            _env = env;
        }

        public async Task< IActionResult> Reports (int Id) //Отчеты по договору
        {
           
            Contract contract = await db.Contracts.FindAsync(Id);
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FindAsync(user.WorkerID);
            if (contract != null && worker != null)
            {
                List<DropDownItem> dropDownNetRegions = new List<DropDownItem>
                {
                    new DropDownItem { Name = "Выберите РЭС", Id = 0 }
                };
                foreach (var item in db.NetRegions.Where(r => r.ContractId == contract.Id))
                {
                    dropDownNetRegions.Add(new DropDownItem { Name = item.Name, Id = item.Id });
                }
                SelectList netRegions = new SelectList(dropDownNetRegions, "Id", "Name");
              
                ViewBag.Contract = contract;
                ViewBag.NetRegions = netRegions;
                
                //Отчеты по ВЛ
                ViewBag.ALReports = await ALReportData(worker.Id, contract.Id);
                var acceptedAlReports = await ALReportData(worker.Id, contract.Id, "принят куратором");
                var importedAlReports = await ALReportData(worker.Id, contract.Id, "импортирован");
                ViewBag.ALAcceptedReports = acceptedAlReports.Concat(importedAlReports).ToList();
         
                //Отчеты по ТП/РП
                ViewBag.SBReports = await SBReportData(worker.Id, contract.Id);
                var acceptedSBReports = await SBReportData(worker.Id, contract.Id, "принят куратором");
                var importedSBReports = await SBReportData(worker.Id, contract.Id, "импортирован");
                ViewBag.SBAcceptedReports = acceptedSBReports.Concat(importedSBReports).ToList();

                //Отчеты по УСПД
                ViewBag.USPDReports = await USPDReportData(worker.Id, contract.Id);
                var acceptedUSPDReports = await USPDReportData(worker.Id, contract.Id, "принят куратором");
                var importedUSPDReports = await USPDReportData(worker.Id, contract.Id, "импортирован");
                ViewBag.USPDAcceptedReports = acceptedUSPDReports.Concat(importedUSPDReports).ToList();

                //Отчеты по Демонтажу
                ViewBag.UnmountReports = await UnmountReportData(worker.Id, contract.Id);
                var acceptedUnmountReports = await UnmountReportData(worker.Id, contract.Id, "принят куратором");
                var importedUnmountReports = await UnmountReportData(worker.Id, contract.Id, "импортирован");
                ViewBag.UnmountAcceptedReports = acceptedUnmountReports.Concat(importedUnmountReports).ToList();


                return View();
            }
            else
            {
                ViewData["error"] = "Договор не найден в БД!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        #region Общие методы для всех типов отчетов
        [HttpPost]
        public async Task<IActionResult> NewReport (int contractId, int netRegionId, string reportType) //Создание отчета 
        {
            Contract contract = await db.Contracts.FindAsync(contractId);
            NetRegion netRegion = await db.NetRegions.FindAsync(netRegionId);

            List<DropDownItem> dropDownCurators = new List<DropDownItem>();
            foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
            {
                dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
            }
            SelectList curators = new SelectList(dropDownCurators, "Id", "Name");
            ViewBag.Curators = curators;

            if (contract == null)
            {
                ViewData["error"] = "Договор не найден в БД!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
            if (netRegion == null)
            {
                ViewData["error"] = "РЭС не найден в БД!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }

            ViewBag.Contract = contract;
            ViewBag.NetRegion = netRegion;

            switch (reportType)
            {
                case "ВЛ":
                    return View("/Views/Report/AL/ALReport.cshtml", new MounterReportUgesAL());
                case "ТП/РП":
                    return View("/Views/Report/SB/SBReport.cshtml", new SBReport());
                case "УСПД":
                    return View("/Views/Report/USPD/USPDReport.cshtml", new USPDReport());
                case "Демонтаж":
                    return View("/Views/Report/Unmount/UnmountReport.cshtml", new UnmountReport());
                default:
                    ViewData["error"] = "Шаблон отчета отсутсвует!";
                    return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> OpenReport(int reportId, string reportType) //Открытие отчета 
        {
            switch (reportType)
            {
                case "ВЛ":
                    return await OpenALReport(reportId);
                case "ТП/РП":
                    return await OpenSBReport(reportId);
                case "УСПД":
                    return await OpenUSPDReport(reportId);
                case "Демонтаж":
                    return await OpenUnmountReport(reportId);
                   
                default:
                    ViewData["error"] = "Шаблон отчета отсутсвует!";
                    return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<string> CheckDevice(string serialNumber, int contractId) //Проверка возможнусти внести ПУ в отчет 
        {
            DeviceCheck check = new DeviceCheck(db);
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);

            return await check.CheckDevice(serialNumber, contractId, worker);
        }

        private async Task<List<ReportRemark>> GetRemarksForReport (int reportId, string reportType) //получить замечания к отчету 
        {
            List<ReportRemark> remarks = new List<ReportRemark>();
            ReportType type = await db.ReportTypes.FirstOrDefaultAsync(t => t.Description == reportType);
            remarks = db.ReportRemarks.Where(r => r.ReportTypeId == type.Id && r.ReportId == reportId).ToList();
            return remarks;
        }

        private async Task<List<ReportComment>> GetReportComments (int reportId, string reportType) //Получить комментарии к отчету 
        {
            List<ReportComment> comments = new List<ReportComment>();
            ReportType type = await db.ReportTypes.FirstOrDefaultAsync(t => t.Description == reportType);
            comments = db.ReportComments.Where(c => c.ReportTypeId == type.Id && c.ReportId == reportId).ToList();
            return comments;

        }

        public IActionResult GenerateReport(int reportId, string reportType) //Генерация EXCEL файла отчета 
        {
            switch (reportType)
            {
                case "ВЛ":
                    return GenerateALReport(reportId);
                case "ТП/РП":
                    return GenerateSBReport(reportId);
                case "УСПД":
                    return GenerateUSPDReport(reportId);
                case "Демонтаж":
                    return GenerateUnmountReport(reportId);
                default:
                    ViewData["error"] = "Шаблон отчета отсутсвует!";
                    return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> AddAdditionalMaterial(int deviceItemId, int materialId, string volume) //Доавление доп материалов к ПУ 
        {
            MounterReportUgesDeviceItem item = await db.MounterReportUgesALItems.FindAsync(deviceItemId);
            Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == item.Serial);
            Material material = await db.Materials.FindAsync(materialId);
            volume = volume.Replace('.', ',');
            double.TryParse(volume, out double dVolume);
            await db.AdditionalMaterials.AddAsync(new AdditionalMaterial { DeviceId = device.Id, Volume = dVolume, Material = material });
            await db.SaveChangesAsync();
            return PartialView("~/Views/Report/AL/_PU.cshtml", db.MounterReportUgesALItems.Where(i => i.KDEId == item.KDEId));
        }

        public async Task<IActionResult> AddComment(int reportId, string reportType, string text) //Добавление комментария к отчету 
        {
            ReportType type = await db.ReportTypes.FirstOrDefaultAsync(r => r.Description == reportType);
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            ReportComment comment = new ReportComment { ReportTypeId = type.Id, UserId = user.Id, ReportId = reportId, Text = text, Date = DateTime.Now };
            await db.ReportComments.AddAsync(comment);
            await db.SaveChangesAsync();
            return PartialView("~/Views/Report/_comments.cshtml", db.ReportComments.Where(r => r.ReportTypeId == type.Id && r.ReportId == reportId));
        }

        public async Task<IActionResult> DeleteAdditionalMaterial(int id, int kdeId) //Удаление доп материала 
        {
            AdditionalMaterial material = await db.AdditionalMaterials.FindAsync(id);
            if (material != null)
            {
                db.AdditionalMaterials.Remove(material);
                await db.SaveChangesAsync();
            }
            return PartialView("~/Views/Report/AL/_PU.cshtml", db.MounterReportUgesALItems.Where(i => i.KDEId == kdeId));
        }

        public async Task<string> DeleteSwitch(int switchId, int reportId, string reportType) // Удаление рубильника (общий метод для ТП/РП и УСПД)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);

            switch (reportType)
            {
                case "SB":
                    SBReport report = await db.SBReports.FindAsync(reportId);
                    if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
                    {
                        Switch @switch = await db.Switches.FindAsync(switchId);
                        if (@switch != null)
                        {
                            Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == @switch.DeviceSerial);
                            device.CurrentState = "выдача со склада";
                            await AddDeviceState("удален из отчета", device.Id);
                            db.Switches.Remove(@switch);
                            await db.SaveChangesAsync();
                            return "";
                        }
                        else return "Невозможно удалить рубильник";
                    }
                    else return "Невозможно изменить отправленный отчет";

                case "USPD":
                    USPDReport uReport = await db.USPDReports.FindAsync(reportId);
                    if (uReport.ReportState.Description == "в работе" || uReport.ReportState.Description == "с замечаниями куратора")
                    {
                        Switch @switch = await db.Switches.FindAsync(switchId);
                        if (@switch != null)
                        {
                            Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == @switch.DeviceSerial);
                            device.CurrentState = "выдача со склада";
                            await AddDeviceState("удален из отчета", device.Id);
                            db.Switches.Remove(@switch);
                            await db.SaveChangesAsync();
                            return "";
                        }
                        else return "Невозможно удалить рубильник";
                    }
                    else return "Невозможно изменить отправленный отчет";

                default:
                    return "Неопределенный тип отчета";
            }



        }

        private async Task AddDeviceState(string state, int deviceId) //Добавление статуса устройства
        {
            User @user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            DeviceStateType stateType = await db.DeviceStateTypes.FirstOrDefaultAsync(s => s.Description == state);
            db.DeviceStates.Add(new DeviceState { DeviceId = deviceId, UserId = user.Id, DeviceStateTypeId = stateType.Id, Date = DateTime.Now });
        }
        #endregion

        #region ALReport //Отчет по ВЛ

        public async Task<IActionResult> OpenALReport(int id) //Открытие отчета ВЛ
        {
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(id);
            Contract contract = await db.Contracts.FindAsync(report.ContractId);
            NetRegion netRegion = await db.NetRegions.FindAsync(report.NetRegionId);
            WorkerManager workerManager = new WorkerManager(db);

            List<DropDownItem> dropDownCurators = new List<DropDownItem>();
            foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
            {
                dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
            }
            SelectList curators = new SelectList(dropDownCurators, "Id", "Name");

            List<DropDownItem> dropDownMaterials = new List<DropDownItem>();
            foreach (var item in workerManager.GetWorkerMaterials(report.WorkerId))
            {
                dropDownMaterials.Add(new DropDownItem { Id = item.Id, Name = item.MaterialType.Name + " [" + item.MaterialType.Unit.Name + "]" });
            }
            SelectList materials = new SelectList(dropDownMaterials, "Id", "Name");

            ViewBag.Remarks = await GetRemarksForReport(report.Id, "ВЛ");
            ViewBag.Curators = curators;
            ViewBag.Materials = materials;
            ViewBag.Contract = contract;
            ViewBag.NetRegion = netRegion;
            ViewBag.Supports = db.PowerLineSupports.Where(s => s.MounterReportUgesALId == id).ToList();
            ViewBag.Comments = await GetReportComments(id, "ВЛ");

            return View("~/Views/Report/AL/ALReport.cshtml", report);
        }

        [HttpPost]
        public async Task<IActionResult> SaveALReport(MounterReportUgesAL report) //Сохранение шапки отчета по ВЛ
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(s => s.Description == "в работе");
            if (worker != null)
            {
                report.WorkerId = worker.Id;
                report.ReportStateId = reportState.Id;
                await db.MounterReportUgesALs.AddAsync(report);
                await db.SaveChangesAsync();
                List<DropDownItem> dropDownCurators = new List<DropDownItem>();
                foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
                {
                    dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
                }
                SelectList curators = new SelectList(dropDownCurators, "Id", "Name");
                ViewBag.Curators = curators;
                ViewBag.Contract = await db.Contracts.FindAsync(report.ContractId);
                ViewBag.NetRegion = await db.NetRegions.FindAsync(report.NetRegionId);
                ViewBag.Supports = db.PowerLineSupports.Where(s => s.MounterReportUgesALId == report.Id).ToList();
                ViewBag.Remarks = new List<ReportRemark>();
                return Redirect("/Report/OpenALReport/" + report.Id);
               
            }
            else
            {
                ViewData["error"] = "Невозможно сохранить отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditALReport(MounterReportUgesAL report) //Шапки отчета по ВЛ
        {

            MounterReportUgesAL oldReport = await db.MounterReportUgesALs.FindAsync(report.Id);
            if (oldReport.ReportState.Description == "в работе" || oldReport.ReportState.Description == "с замечаниями куратора")
            {
                oldReport.Brigade = report.Brigade;
                oldReport.Date = report.Date;
                oldReport.Fider = report.Fider;
                oldReport.Local = report.Local;
                oldReport.Substation = report.Substation;
                oldReport.WorkPermit = report.WorkPermit;

                await db.SaveChangesAsync();

                //List<DropDownItem> dropDownCurators = new List<DropDownItem>();
                //foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
                //{
                //    dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
                //}
                //SelectList curators = new SelectList(dropDownCurators, "Id", "Name");
                //ViewBag.Curators = curators;
                //ViewBag.Remarks = await GetRemarksForReport(oldReport.Id, "ВЛ");
                //ViewBag.Comments = await GetReportComments(report.Id, "ВЛ");
                //ViewBag.Contract = await db.Contracts.FindAsync(report.ContractId);
                //ViewBag.NetRegion = await db.NetRegions.FindAsync(report.NetRegionId);
                //ViewBag.Supports = db.PowerLineSupports.Where(s => s.MounterReportUgesALId == report.Id).ToList();
                return RedirectToAction("OpenALReport", "Report", new { id = report.Id });
                
            }
            else
            {
                ViewData["error"] = "Невозможно сохранить отправленный отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddSupport (int supportNumber, string powerLineType, int fixatorsCount, int reportId) //Добавление опоры в отчет
        {
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(reportId);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                PowerLineSupport support = new PowerLineSupport { SupportNumber = supportNumber, PowerLineType = powerLineType, FixatorsCount = fixatorsCount, MounterReportUgesALId = reportId };
                await db.PowerLineSupports.AddAsync(support);
                await db.SaveChangesAsync();
                return PartialView("~/Views/Report/AL/_PowerLineSupports.cshtml", db.PowerLineSupports.Where(s => s.MounterReportUgesALId == reportId));
            }
            else
            {
                ViewData["error"] = "Невозможно изменить отправленный отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> DeleteSupport(int supportNumber) //Удалить опору из отчета с удалением КДЕ и ПУ 
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            PowerLineSupport support = await db.PowerLineSupports.FindAsync(supportNumber);
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(support.MounterReportUgesALId);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                foreach (var kde in support.KDEs)
                {
                    foreach (var item in kde.MounterReportUgesDeviceItems)
                    {
                        Device device = await db.Devices.FindAsync(item.DeviceId);
                        device.CurrentState = "выдача со склада";
                        await AddDeviceState("удален из отчета", device.Id);
                        db.AdditionalMaterials.RemoveRange(db.AdditionalMaterials.Where(m => m.DeviceId == device.Id));
                        await db.SaveChangesAsync();
                    }
                    db.MounterReportUgesALItems.RemoveRange(db.MounterReportUgesALItems.Where(o => o.KDEId == kde.Id));
                }
                db.KDEs.RemoveRange(db.KDEs.Where(k => k.PowerLineSupportId == support.Id));
                db.PowerLineSupports.Remove(support);
                await db.SaveChangesAsync();
                return PartialView("~/Views/Report/AL/_PowerLineSupports.cshtml", db.PowerLineSupports.Where(s => s.MounterReportUgesALId == support.MounterReportUgesALId));
            }
            else
            {
                ViewData["error"] = "Невозможно изменить отправленный отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }

        }

        [HttpPost]
        public async Task<IActionResult> AddKde(int supportNumber, string kdeType) //добавление КДЕ в отчет
        {
            PowerLineSupport support = await db.PowerLineSupports.FindAsync(supportNumber);
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(support.MounterReportUgesALId);

            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                KDEType type = await db.KDETypes.FirstOrDefaultAsync(t => t.Name == kdeType);
                KDE kde = new KDE { KDETypeId = type.Id, PowerLineSupportId = supportNumber };
                await db.KDEs.AddAsync(kde);
                await db.SaveChangesAsync();
                return PartialView("~/Views/Report/AL/_KDE.cshtml", kde);
            }
            else
            {
                ViewData["error"] = "Невозможно изменить отправленный отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        [HttpPost]
        public async Task<string> DeleteKde(int kdeID) //Удаление КДЕ с ПУ
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            KDE kde = await db.KDEs.FindAsync(kdeID);
            PowerLineSupport support = await db.PowerLineSupports.FindAsync(kde.PowerLineSupportId);
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(support.MounterReportUgesALId);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                foreach (var item in kde.MounterReportUgesDeviceItems)
                {
                    Device device = await db.Devices.FindAsync(item.DeviceId);
                    device.CurrentState = "выдача со склада";
                    await AddDeviceState("удален из отчета", device.Id);
                    db.AdditionalMaterials.RemoveRange(db.AdditionalMaterials.Where(m => m.DeviceId == device.Id));
                    await db.SaveChangesAsync();
                }
                db.MounterReportUgesALItems.RemoveRange(db.MounterReportUgesALItems.Where(o => o.KDEId == kde.Id));
                db.KDEs.Remove(kde);
                await db.SaveChangesAsync();
                return "";
            }
            else
            {
                return  "Невозможно изменить отправленный отчет!";
            }

        }

        [HttpPost]
        public async Task<IActionResult> AddDALPU(MounterReportUgesDeviceItem deviceItem)//Добавление ПУ в отчет 
        {
            Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == deviceItem.Serial);
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            KDE kde = await db.KDEs.FindAsync(deviceItem.KDEId);
            PowerLineSupport support = await db.PowerLineSupports.FindAsync(kde.PowerLineSupportId);
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(support.MounterReportUgesALId);

            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {

                DeviceCheck check = new DeviceCheck(db);
                string error = check.CheckDevice(device, worker, kde);
                if (error == "")
                {
                    deviceItem.WorkerId = worker.Id;
                    deviceItem.DeviceId = device.Id;
                    await db.MounterReportUgesALItems.AddAsync(deviceItem);
                    device.CurrentState = "включен в отчет";
                    await AddDeviceState("включен в отчет", device.Id);
                    db.SaveChanges();
                }
                else
                {
                    ViewData["error"] = error;
                    //return View("~/Views/Shared/ErrorPage.cshtml");
                }
            }
            else
                ViewData["error"] = "Невозможно изменить отправленный отчет!";

            return PartialView("~/Views/Report/AL/_PU.cshtml", db.MounterReportUgesALItems.Where(i => i.KDEId == deviceItem.KDEId));
        }

        public async Task<IActionResult> DeleteALPU(int id) //Удаление ПУ из отчета
        {
            MounterReportUgesDeviceItem deviceItem = await db.MounterReportUgesALItems.FindAsync(id);

            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            KDE kde = await db.KDEs.FindAsync(deviceItem.KDEId);
            PowerLineSupport support = await db.PowerLineSupports.FindAsync(kde.PowerLineSupportId);
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(support.MounterReportUgesALId);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == deviceItem.Serial);
                device.CurrentState = "выдача со склада";
                await AddDeviceState("удален из отчета", device.Id);
                db.MounterReportUgesALItems.Remove(deviceItem);
                db.AdditionalMaterials.RemoveRange(db.AdditionalMaterials.Where(m => m.DeviceId == device.Id));
                await db.SaveChangesAsync();
                return PartialView("~/Views/Report/AL/_PU.cshtml", db.MounterReportUgesALItems.Where(i => i.KDEId == deviceItem.KDEId));
            }
            else
            {
                ViewData["error"] = "Невозможно изменить отправленный отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditSupport (int supportNumber, string powerLineType, int fixatorsCount, int id)
        {
            PowerLineSupport support = await db.PowerLineSupports.FindAsync(id);
            if(support != null)
            {
                support.SupportNumber = supportNumber;
                support.PowerLineType = powerLineType;
                support.FixatorsCount = fixatorsCount;
                await db.SaveChangesAsync();
            }
            return PartialView("~/Views/Report/AL/_PowerLineSupports.cshtml", db.PowerLineSupports.Where(s => s.MounterReportUgesALId == support.MounterReportUgesALId));
        }

        [HttpPost]
        public string CheckKDE(int kdeId) //Проверка возможности добавить ПУ в КДЕ 
        {
            KDE kde = db.KDEs.Find(kdeId);
            if (kde != null)
            {
                if (kde.KDEType.Name != "КДЕ-3-2" && kde.MounterReportUgesDeviceItems.Count > 0)
                    return "В КДЕ максимально допустимое кол-во ПУ!";
                if (kde.KDEType.Name == "КДЕ-3-2" && kde.MounterReportUgesDeviceItems.Count >= 2)
                    return "В КДЕ максимально допустимое кол-во ПУ!";
                return "";
            }
            else return "КДЕ не найден в БД!";
        }
        
        [HttpPost]
        public async Task<string> DeleteALReport (int id) //Удаление отчета по ВЛ
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FindAsync(user.WorkerID);
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(id);
            Contract contract = await db.Contracts.FindAsync(report.ContractId);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                foreach (var support in report.PowerLineSupports)
                {
                    foreach (var kde in support.KDEs)
                    {
                        foreach (var item in kde.MounterReportUgesDeviceItems)
                        {
                            Device device = await db.Devices.FindAsync(item.DeviceId);
                            db.AdditionalMaterials.RemoveRange(db.AdditionalMaterials.Where(m => m.DeviceId == device.Id));
                            device.CurrentState = "выдача со склада";
                            await AddDeviceState("удален из отчета", device.Id);
                            await db.SaveChangesAsync();
                        }
                        db.MounterReportUgesALItems.RemoveRange(db.MounterReportUgesALItems.Where(o => o.KDEId == kde.Id));
                    }
                    db.KDEs.RemoveRange(db.KDEs.Where(k => k.PowerLineSupportId == support.Id));
                }
                db.PowerLineSupports.RemoveRange(db.PowerLineSupports.Where(s => s.MounterReportUgesALId == id));
                db.ReportComments.RemoveRange(db.ReportComments.Where(c => c.ReportId == report.Id && c.ReportType.Description == "ВЛ"));
                db.MounterReportUgesALs.Remove(report);
                await db.SaveChangesAsync();

                return "";
            }
            else return "Невозможно удалить отправленный отчет!";
           
        }

        public async Task<string> SendAlReport (int id, int curatorId) //Отправка отчета куратору на провекру
        {
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(id);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(r => r.Description == "отправлен куратору");
                report.ReportStateId = reportState.Id;
                report.CuratorId = curatorId;
                await db.SaveChangesAsync();
                return "Отчет отправлен!";
            }
            else return "Невозможно отправить отчет!";
            
        }

        public IActionResult GenerateALReport(int id)
        {
            ReportGenerator reportGenerator = new ReportGenerator(db, _env);
            string filePath =  reportGenerator.GenerateAlReport(id);
            return PhysicalFile(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Отчет ВЛ №" + id +".xlsx");
        }
        
        public async Task<string> EditALPU (int id)
        {
            MounterReportUgesDeviceItem pu = await db.MounterReportUgesALItems.FindAsync(id);
            pu.Device = null;
            pu.DeviceId = 0;
            pu.KDE = null;
            pu.KDEId = 0;
            pu.Worker = null;
            pu.WorkerId = 0;
            return JsonConvert.SerializeObject(pu);
        }

        [HttpPost]
        public async Task<IActionResult> EditDAlPU (MounterReportUgesDeviceItem deviceItem)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            // Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            MounterReportUgesDeviceItem pu = await db.MounterReportUgesALItems.FirstOrDefaultAsync(d => d.Serial == deviceItem.Serial);
            KDE kde = await db.KDEs.FindAsync(pu.KDEId);
            PowerLineSupport support = await db.PowerLineSupports.FindAsync(kde.PowerLineSupportId);
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(support.MounterReportUgesALId);

            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
               
                
                if (pu != null)
                {
                    pu.Street = deviceItem.Street;
                    pu.House = deviceItem.House;
                    pu.Building = deviceItem.Building;
                    pu.Flat = deviceItem.Flat;
                    pu.DeviceSeal = deviceItem.DeviceSeal;
                    pu.Sum = deviceItem.Sum;
                    pu.T1 = deviceItem.T1;
                    pu.T2 = deviceItem.T2;
                    pu.U1 = deviceItem.U1;
                    pu.U2 = deviceItem.U2;
                    pu.U3 = deviceItem.U3;
                    pu.WireConsumptionUpDown = deviceItem.WireConsumptionUpDown;
                    pu.WireConsumptionNewInput = deviceItem.WireConsumptionNewInput;
                    pu.KDESeal = deviceItem.KDESeal;
                    pu.SwitchSeal = deviceItem.SwitchSeal;
                    pu.PhoneNumber = deviceItem.PhoneNumber;
                    pu.InstallPlace = deviceItem.InstallPlace;
                    pu.PowerLineType = deviceItem.PowerLineType;
                    db.SaveChanges();
                }
                else
                {
                    ViewData["error"] = "ПУ не найден в БД!";
                    //return View("~/Views/Shared/ErrorPage.cshtml");
                }
            }
            else
                ViewData["error"] = "Невозможно изменить отправленный отчет!";

            return PartialView("~/Views/Report/AL/_PU.cshtml", db.MounterReportUgesALItems.Where(i => i.KDEId == pu.KDEId));
        }

        private async Task<List<ReportItem>> ALReportData (int workerId, int contractId)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.MounterReportUgesALs.Where(r => r.WorkerId == workerId && r.ContractId == contractId && r.ReportState.Description != "принят куратором" && r.ReportState.Description != "импортирован"))
            {
                foreach (var support in item.PowerLineSupports)
                {
                    foreach (var kde in support.KDEs)
                    {
                        foreach (var device in kde.MounterReportUgesDeviceItems)
                        {
                            Device pu = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == device.Serial);
                            if (pu.CurrentState == "привязан к ту")
                                accepted++;
                            else mounted++;
                        }
                           
                    }
                }
                reports.Add(new ReportItem { Substation = item.Substation, Id = item.Id, Mounted = mounted, Accepted = accepted, NetRegion = item.NetRegion.Name, Date = item.Date, Type = "ВЛ" , State = item.ReportState.Description});
                mounted = 0;
                accepted = 0;
            }
            return reports;
        }

        private async Task<List<ReportItem>> ALReportData(int workerId, int contractId, string state)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.MounterReportUgesALs.Where(r => r.WorkerId == workerId && r.ContractId == contractId && (r.ReportState.Description == state) ))
            {
                foreach (var support in item.PowerLineSupports)
                {
                    foreach (var kde in support.KDEs)
                    {
                        foreach (var device in kde.MounterReportUgesDeviceItems)
                        {
                            Device pu = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == device.Serial);
                            if (pu.CurrentState == "привязан к ту")
                                accepted++;
                            else mounted++;
                        }

                    }
                }
                reports.Add(new ReportItem { Substation = item.Substation, Id = item.Id, Mounted = mounted, Accepted = accepted, NetRegion = item.NetRegion.Name, Date = item.Date, Type = "ВЛ", State = item.ReportState.Description });
            }
            return reports;
        }
  
        #endregion

        #region SBReport //Отчет по ТП/РП

        public async Task<IActionResult> OpenSBReport (int id)
        {
            SBReport report = await db.SBReports.FindAsync(id);
            Contract contract = await db.Contracts.FindAsync(report.ContractId);
            NetRegion netRegion = await db.NetRegions.FindAsync(report.NetRegionId);

            List<DropDownItem> dropDownCurators = new List<DropDownItem>();

            foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
            {
                dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
            }
            SelectList curators = new SelectList(dropDownCurators, "Id", "Name");

            ViewBag.Remarks = await GetRemarksForReport(report.Id, "ТП/РП");
            ViewBag.Curators = curators;
            ViewBag.Contract = contract;
            ViewBag.NetRegion = netRegion;
            ViewBag.Comments = await GetReportComments(report.Id, "ТП/РП");
           
            return View("~/Views/Report/SB/SBReport.cshtml", report);
        }

        public async Task<IActionResult> SaveSBReport (SBReport report)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(s => s.Description == "в работе");
            if (worker != null)
            {
                report.WorkerId = worker.Id;
                report.ReportStateId = reportState.Id;
                await db.SBReports.AddAsync(report);
                await db.SaveChangesAsync();
                List<DropDownItem> dropDownCurators = new List<DropDownItem>();
                foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
                {
                    dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
                }
                SelectList curators = new SelectList(dropDownCurators, "Id", "Name");
                ViewBag.Curators = curators;
                ViewBag.Contract = await db.Contracts.FindAsync(report.ContractId);
           
                ViewBag.NetRegion = await db.NetRegions.FindAsync(report.NetRegionId);
                ViewBag.Remarks = new List<ReportRemark>();
                return Redirect("/Report/OpenSBReport/" + report.Id);
            }
            else
            {
                ViewData["error"] = "Невозможно сохранить отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> EditSBReport(SBReport report) //
        {

            SBReport oldReport = await db.SBReports.FindAsync(report.Id);
            if (oldReport.ReportState.Description == "в работе" || oldReport.ReportState.Description == "с замечаниями куратора")
            {
                oldReport.Brigade = report.Brigade;
                oldReport.Date = report.Date;
               
                oldReport.WorkPermit = report.WorkPermit;
                oldReport.Phase = report.Phase;
                

                oldReport.PhoneNumber = report.PhoneNumber;
                oldReport.MeterBoard = report.MeterBoard;
                oldReport.Substation = report.Substation;

                List<DropDownItem> dropDownCurators = new List<DropDownItem>();
                foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
                {
                    dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
                }
                SelectList curators = new SelectList(dropDownCurators, "Id", "Name");
                ViewBag.Curators = curators;


                await db.SaveChangesAsync();
                ViewBag.Remarks = await GetRemarksForReport(oldReport.Id, "ТП/РП");
                ViewBag.Contract = await db.Contracts.FindAsync(report.ContractId);
                ViewBag.NetRegion = await db.NetRegions.FindAsync(report.NetRegionId);
               
                return View("/Views/Report/SB/SBReport.cshtml", oldReport);
            }
            else
            {
                ViewData["error"] = "Невозможно сохранить отправленный отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> AddSwitchSB ( Switch _switch)
        {
            Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == _switch.DeviceSerial);
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            SBReport report = await db.SBReports.FindAsync(_switch.SBReportId);
            List<Switch> switches = new List<Switch>();
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {

                DeviceCheck check = new DeviceCheck(db);
                string error = await check.CheckDevice(_switch.DeviceSerial, report.ContractId, worker);
                if (error == "")
                {
                    await db.Switches.AddAsync(_switch);
                    device.CurrentState = "включен в отчет";
                    await AddDeviceState("включен в отчет", device.Id);
                    db.SaveChanges();
                    switches = db.Switches.Where(s => s.SBReportId == _switch.SBReportId).ToList();
                }
                else
                {
                    ViewData["error"] = error;
                }
            }
            else ViewData["error"] = "Невозможно изменить отправленный отчет!";

            return PartialView("~/Views/Report/SB/_SwitchesTable.cshtml", switches);
        }

        public async Task<string> DeleteSBReport(int id)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            SBReport report = await db.SBReports.FindAsync(id);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                foreach (var @switch in report.Switches)
                {
                    Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == @switch.DeviceSerial);
                    device.CurrentState = "выдача со склада";
                    await AddDeviceState("удален из отчета",device.Id);
                }
                db.Switches.RemoveRange(db.Switches.Where(s => s.SBReportId == report.Id));
                db.SBReports.Remove(report);
                await db.SaveChangesAsync();
                return "";
            }
            else return "Невозможно удалить отправленный отчет";
        }

        public IActionResult GenerateSBReport (int id)
        {
            ReportGenerator reportGenerator = new ReportGenerator(db, _env);
            string filePath = reportGenerator.GenerateSBReport(id);
            return PhysicalFile(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Отчет ТПРП №" + id + ".xlsx");
        }

        public async Task<string> SendSBReport (int id, int curatorId)
        {
            SBReport report = await db.SBReports.FindAsync(id);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(r => r.Description == "отправлен куратору");
                report.ReportStateId = reportState.Id;
                report.CuratorId = curatorId;
                await db.SaveChangesAsync();
                return "Отчет отправлен!";
            }
            else return "Невозможно отправить отчет!";
        }
        
        private async Task<List<ReportItem>> SBReportData(int workerId, int contractId)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.SBReports.Where(r => r.WorkerId == workerId && r.ContractId == contractId && r.ReportState.Description != "принят куратором" && r.ReportState.Description != "импортирован"))
            {
                mounted = item.Switches.Count;
                reports.Add(new ReportItem { Substation = item.Substation, Id = item.Id, Mounted = mounted, Accepted = accepted, NetRegion = item.NetRegion.Name, Date = item.Date, Type = "ТП/РП", State = item.ReportState.Description });
                mounted = 0;
                accepted = 0;
            }
            return reports;
        }

        private async Task<List<ReportItem>> SBReportData(int workerId, int contractId, string state)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.SBReports.Where(r => r.WorkerId == workerId && r.ContractId == contractId && r.ReportState.Description == state))
            {
                mounted = item.Switches.Count;
                reports.Add(new ReportItem { Substation = item.Substation, Id = item.Id, Mounted = mounted, Accepted = accepted, NetRegion = item.NetRegion.Name, Date = item.Date, Type = "ТП/РП", State = item.ReportState.Description });
                mounted = 0;
                accepted = 0;
            }
            return reports;
        }

        #endregion

        #region USPDReport //Отчет по УСПД

        public async Task<IActionResult> OpenUSPDReport(int id)
        {
            USPDReport report = await db.USPDReports.FindAsync(id);
            Contract contract = await db.Contracts.FindAsync(report.ContractId);
            NetRegion netRegion = await db.NetRegions.FindAsync(report.NetRegionId);

            List<DropDownItem> dropDownCurators = new List<DropDownItem>();

            foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
            {
                dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
            }
            SelectList curators = new SelectList(dropDownCurators, "Id", "Name");

            ViewBag.Remarks = await GetRemarksForReport(report.Id, "УСПД");
            ViewBag.Curators = curators;
            ViewBag.Contract = contract;
            ViewBag.NetRegion = netRegion;
            ViewBag.Comments = await GetReportComments(report.Id, "УСПД");

            return View("~/Views/Report/USPD/USPDReport.cshtml", report);
        }

        public async Task<IActionResult> SaveUSPDReport (USPDReport report)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(s => s.Description == "в работе");
            if (worker != null)
            {
                report.WorkerId = worker.Id;
                report.ReportStateId = reportState.Id;
                await db.USPDReports.AddAsync(report);
                await db.SaveChangesAsync();
                List<DropDownItem> dropDownCurators = new List<DropDownItem>();
                foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
                {
                    dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
                }

                Device uspd = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == report.UspdSerial);
                Device plc = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == report.PlcSerial);
                if (uspd != null && plc != null)
                {
                    uspd.AddState(DeviceStateTypeName.AddToReport, user.Id, null);
                    plc.AddState(DeviceStateTypeName.AddToReport, user.Id, null);
                    await db.SaveChangesAsync();
                }
                else
                {
                    ViewData["error"] = "Невозможно сохранить отчет, УСПД и/или PLC модем не найден в БД!";
                    return View("~/Views/Shared/_ErrorPage.cshtml");
                }


                SelectList curators = new SelectList(dropDownCurators, "Id", "Name");
                ViewBag.Curators = curators;
                ViewBag.Contract = await db.Contracts.FindAsync(report.ContractId);

                ViewBag.NetRegion = await db.NetRegions.FindAsync(report.NetRegionId);
                ViewBag.Remarks = new List<ReportRemark>();
                return Redirect("/Report/OpenUSPDReport/" + report.Id);
            }
            else
            {
                ViewData["error"] = "Невозможно сохранить отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> EditUSPDReport (USPDReport report)
        {
            USPDReport oldReport = await db.USPDReports.FindAsync(report.Id);
            if (oldReport.ReportState.Description == "в работе" || oldReport.ReportState.Description == "с замечаниями куратора")
            {
                oldReport.Brigade = report.Brigade;
                oldReport.Date = report.Date;
                oldReport.PhoneNumber = report.PhoneNumber;
                oldReport.Substation = report.Substation;
                oldReport.AVR = report.AVR;
                oldReport.Ftp = report.Ftp;
                oldReport.Gofr = report.Gofr;
                oldReport.Kvvg = report.Kvvg;
                oldReport.Local = report.Local;
                oldReport.PlcSerial = report.PlcSerial;
                oldReport.UspdSerial = report.UspdSerial;
                
                
                List<DropDownItem> dropDownCurators = new List<DropDownItem>();
                foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
                {
                    dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
                }
                SelectList curators = new SelectList(dropDownCurators, "Id", "Name");
                ViewBag.Curators = curators;


                await db.SaveChangesAsync();
                ViewBag.Remarks = await GetRemarksForReport(oldReport.Id, "ТП/РП");
                ViewBag.Contract = await db.Contracts.FindAsync(report.ContractId);
                ViewBag.NetRegion = await db.NetRegions.FindAsync(report.NetRegionId);

                return View("/Views/Report/USPD/USPDReport.cshtml", oldReport);
            }
            else
            {
                ViewData["error"] = "Невозможно сохранить отправленный отчет!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> AddSwitchUSPD (Switch _switch)
        {
            Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == _switch.DeviceSerial);
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            USPDReport report = await db.USPDReports.FindAsync(_switch.USPDReportId);
            List<Switch> switches = new List<Switch>();
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                if (report.Switches.Count < 2)
                {
                    DeviceCheck check = new DeviceCheck(db);
                    string error = await check.CheckDevice(_switch.DeviceSerial, report.ContractId, worker);
                    if (error == "")
                    {
                        await db.Switches.AddAsync(_switch);
                        device.CurrentState = "включен в отчет";
                        await AddDeviceState("включен в отчет", device.Id);
                        db.SaveChanges();
                        switches = db.Switches.Where(s => s.USPDReportId == _switch.USPDReportId).ToList();
                    }
                    else
                    {
                        ViewData["error"] = error;
                    }
                }
                else
                {
                    switches = db.Switches.Where(s => s.USPDReportId == _switch.USPDReportId).ToList();
                    return PartialView("~/Views/Report/SB/_SwitchesTable.cshtml", switches);
                }
            }
            else ViewData["error"] = "Невозможно изменить отправленный отчет!";

            return PartialView("~/Views/Report/SB/_SwitchesTable.cshtml", switches);
        }

        public IActionResult GenerateUSPDReport(int id)
        {
            ReportGenerator reportGenerator = new ReportGenerator(db, _env);
            string filePath = reportGenerator.GenerateUSPDReport(id);
            return PhysicalFile(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Отчет УСПД №" + id + ".xlsx");
        }

        public async Task<string> DeleteUSPDReport(int id)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            USPDReport report = await db.USPDReports.FindAsync(id);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                foreach (var @switch in report.Switches)
                {
                    Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == @switch.DeviceSerial);
                    device.CurrentState = "выдача со склада";
                    await AddDeviceState("удален из отчета", device.Id);
                }
                db.Switches.RemoveRange(db.Switches.Where(s => s.USPDReportId == report.Id));
                db.USPDReports.Remove(report);
                Device uspd = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == report.UspdSerial);
                Device plc = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == report.PlcSerial);
                if (uspd != null && plc != null)
                {
                    uspd.AddState(DeviceStateTypeName.DeleteFromReport, user.Id, null);
                    plc.AddState(DeviceStateTypeName.DeleteFromReport, user.Id, null);
                }
                else return "Невозможно удалть отчет";
                await db.SaveChangesAsync();
                return "";
            }
            else return "Невозможно удалить отправленный отчет";
        }

        public async Task<string> SendUSPDReport(int id, int curatorId)
        {
            USPDReport report = await db.USPDReports.FindAsync(id);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(r => r.Description == "отправлен куратору");
                report.ReportStateId = reportState.Id;
                report.CuratorId = curatorId;
                await db.SaveChangesAsync();
                return "Отчет отправлен!";
            }
            else return "Невозможно отправить отчет!";
        }

        public async Task<string> CheckUSPD (string serial, int contractId) //Проверка УСПД
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            DeviceCheck check = new DeviceCheck(db);
            Device uspd = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serial);
            if (uspd == null)
                return "Оборудование не найдено в БД!";
            if (!uspd.DeviceType.Type.ToLower().Contains("успд"))
                return "Оборудование не является УСПД!";
            return await check.CheckDevice(serial, contractId, worker);
        }

        public async Task<string> CheckPLC(string serial, int contractId) //Проверка УСПД
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
            DeviceCheck check = new DeviceCheck(db);
            Device plc = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serial);
            if (plc == null)
                return "Оборудование не найдено в БД!";

            if (!plc.DeviceType.Type.ToLower().Contains("plc модем"))
                return "Оборудование не является plc-модемом!";
            return await check.CheckDevice(serial, contractId, worker);
        }

        private async Task<List<ReportItem>> USPDReportData(int workerId, int contractId)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.USPDReports.Where(r => r.WorkerId == workerId && r.ContractId == contractId && r.ReportState.Description != "принят куратором" && r.ReportState.Description != "импортирован"))
            {
                mounted = item.Switches.Count;
                reports.Add(new ReportItem { Substation = item.Substation, Id = item.Id, Mounted = mounted, Accepted = accepted, NetRegion = item.NetRegion.Name, Date = item.Date, Type = "УСПД", State = item.ReportState.Description });
                mounted = 0;
                accepted = 0;
            }
            return reports;
        }

        private async Task<List<ReportItem>> USPDReportData(int workerId, int contractId, string state)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.USPDReports.Where(r => r.WorkerId == workerId && r.ContractId == contractId && r.ReportState.Description == state))
            {
                mounted = item.Switches.Count;
                reports.Add(new ReportItem { Substation = item.Substation, Id = item.Id, Mounted = mounted, Accepted = accepted, NetRegion = item.NetRegion.Name, Date = item.Date, Type = "УСПД", State = item.ReportState.Description });
                mounted = 0;
                accepted = 0;
            }
            return reports;
        }

        #endregion

        #region UnmountReport //отчет по демонтажу

        public async Task<IActionResult> SaveUnmountReport(UnmountReport report)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FirstOrDefaultAsync(w => (w.Surname + " " + w.Name + " " + w.MIddlename) == user.Name);
           
            ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(s => s.Description == "в работе");
            if (worker != null)
            {
                report.WorkerId = worker.Id;
                report.ReportStateId = reportState.Id;
                await db.UnmountReports.AddAsync(report);
                await db.SaveChangesAsync();
            }
           
           
            return Redirect("/Report/OpenUnmountReport/" + report.Id);
        }

        public async Task<IActionResult> OpenUnmountReport( int Id)
        {
            UnmountReport report = await db.UnmountReports.FindAsync(Id);
            Contract contract = await db.Contracts.FindAsync(report.ContractId);
            NetRegion netRegion;
            if (report.NetRegionId != null && report.NetRegionId != 0)
                netRegion = db.NetRegions.Find(report.NetRegionId);
            else netRegion = db.NetRegions.FirstOrDefault();

            List<DropDownItem> dropDownCurators = new List<DropDownItem>();

            foreach (var item in db.Users.Where(p => p.Role.Name == "curator"))
            {
                dropDownCurators.Add(new DropDownItem { Id = item.Id, Name = item.Name });
            }
            SelectList curators = new SelectList(dropDownCurators, "Id", "Name");

            ViewBag.Remarks = await GetRemarksForReport(report.Id, "Демонтаж");
            ViewBag.Curators = curators;
            ViewBag.Contract = contract;
            ViewBag.UnmountedDevices = await GetUnmountedDevices(Id);
            ViewBag.Comments = await GetReportComments(report.Id, "Демонтаж");
            ViewBag.NetRegion = netRegion;
            return View("~/Views/Report/Unmount/UnmountReport.cshtml", report);
        }

        public async Task<string> CheckUnmountedDevice (string serial)
        {
            Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serial);
            if (device == null) return "Оборудование не найдено в БД!";
            RegPoint regPoint = await db.RegPoints.FirstOrDefaultAsync(r => r.DeviceId == device.Id && r.Status != RegPointStatus.Demounted);
            if (regPoint == null) return "Оборудование не привязано к ТУ!";
            //UnmountedDevice unmountedDevice = await db.UnmountedDevices.FirstOrDefaultAsync(d => d.DeviceId == device.Id);
            //if(unmountedDevice != null)
            return "";
                
        }

        public async Task<IActionResult> AddUnmountedDevice (string serial, string reason, int reportId)
        {
            UnmountReport report = await db.UnmountReports.FindAsync(reportId);
            if (report.ReportState.Description == ReportStateTypeName.InWork.ToString() || report.ReportState.Description == ReportStateTypeName.RemarksByCurator.ToString())
            {
                Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serial);
                if (device.ContractId == report.ContractId)
                {
                    UnmountedDevice unmountedDevice = new UnmountedDevice() { DeviceId = device.Id, UnmountReportId = reportId, Reason = reason };
                    await db.UnmountedDevices.AddAsync(unmountedDevice);
                    await AddDeviceState("включен в отчет", device.Id);
                    await db.SaveChangesAsync();
                   
                }
            }
            return PartialView("~/Views/Report/Unmount/_UnmountedDevices.cshtml", await GetUnmountedDevices(reportId));
        }

        public async Task<IActionResult> DeleteUnmountedDevice(int id)
        {
            UnmountedDevice unmountedDevice = await db.UnmountedDevices.FindAsync(id);
            if(unmountedDevice != null && (unmountedDevice.UnmountReport.ReportState.Description == ReportStateTypeName.InWork.ToString() || unmountedDevice.UnmountReport.ReportState.Description == ReportStateTypeName.RemarksByCurator.ToString()))
            {
                db.UnmountedDevices.Remove(unmountedDevice);
                await AddDeviceState("удален из отчета", unmountedDevice.DeviceId);
                await db.SaveChangesAsync();
            }
            return PartialView("~/Views/Report/Unmount/_UnmountedDevices.cshtml", await GetUnmountedDevices(unmountedDevice.UnmountReportId));
        }

        public async Task<string> SendUnmountReport (int id, int curatorId)
        {
            UnmountReport report = await db.UnmountReports.FindAsync(id);
            if (report != null && (report.ReportState.Description == ReportStateTypeName.InWork.ToString() || report.ReportState.Description == ReportStateTypeName.RemarksByCurator.ToString()))
            {
                ReportState state = await db.ReportStates.FirstOrDefaultAsync(s => s.Description == ReportStateTypeName.SentToCurator.ToString());
                report.ReportStateId = state.Id;
                report.CuratorId = curatorId;
                await db.SaveChangesAsync();
                return "Отчет отправлен!";
            }
            else return "Невозможно отправить этот отчет!";
        }

        public async Task<string> DeleteUnmountReport (int id)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            UnmountReport report = await db.UnmountReports.FindAsync(id);
            if (report.ReportState.Description == "в работе" || report.ReportState.Description == "с замечаниями куратора")
            {
                foreach (var pu in report.UnmountedDevices)
                {
                    Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == pu.Device.SerialNumber);
                    await AddDeviceState("удален из отчета", device.Id);
                }
                db.UnmountedDevices.RemoveRange(db.UnmountedDevices.Where(s => s.UnmountReportId == report.Id));
                db.UnmountReports.Remove(report);
                await db.SaveChangesAsync();
                return "";
            }
            else return "Невозможно удалить отправленный отчет";
        }

        private async Task<List<UnmountedDeviceModel>> GetUnmountedDevices (int reportId)
        {
            List<UnmountedDeviceModel> result = new List<UnmountedDeviceModel>();
            var unmountedDevices = from d in db.UnmountedDevices
                                   from p in db.RegPoints
                                   where d.UnmountReportId == reportId && p.DeviceId == d.DeviceId
                                   select new
                                   {
                                       d,
                                       d.Device,
                                       p.Substation,
                                       p.Substation.NetRegion
                                   };
            var devices = unmountedDevices.ToList();
            foreach (var item in devices)
            {
                result.Add(new UnmountedDeviceModel { Id = item.d.Id, Serial = item.Device.SerialNumber, Substation = item.Substation.Name, Reason= item.d.Reason, NetRegion = item.NetRegion.Name });
            }
            return result;
        }

        private async Task<List<ReportItem>> UnmountReportData(int workerId, int contractId)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.UnmountReports.Where(r => r.WorkerId == workerId && r.ContractId == contractId && r.ReportState.Description != "принят куратором" && r.ReportState.Description != "импортирован"))
            {
                mounted = item.UnmountedDevices.Count();
                reports.Add(new ReportItem {Id = item.Id, Mounted = mounted, Accepted = accepted, Date = item.Date, Type = "Демонтаж", State = item.ReportState.Description });
                mounted = 0;
                accepted = 0;
            }
            return reports;
        }

        private async Task<List<ReportItem>> UnmountReportData(int workerId, int contractId, string state)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.UnmountReports.Where(r => r.WorkerId == workerId && r.ContractId == contractId && r.ReportState.Description == state))
            {
                mounted = item.UnmountedDevices.Count();
                reports.Add(new ReportItem { Id = item.Id, Mounted = mounted, Accepted = accepted, Date = item.Date, Type = "Демонтаж", State = item.ReportState.Description });
                mounted = 0;
                accepted = 0;
            }
            return reports;
        }


        public IActionResult GenerateUnmountReport(int id)
        {
            ReportGenerator reportGenerator = new ReportGenerator(db, _env);
            string filePath = reportGenerator.GenerateUnmountReport(id);
            return PhysicalFile(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Отчет Демонтаж №" + id + ".xlsx");
        }


        #endregion
    }
}