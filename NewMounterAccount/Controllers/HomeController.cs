using DbManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NewMounterAccount.AppCode;
using NewMounterAccount.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace NewMounterAccount.Controllers
{
    public class HomeController : Controller
    {
        readonly IHostingEnvironment _env;
        StoreContext db;
        public HomeController(StoreContext context, IHostingEnvironment env)
        {
            db = context;
            _env = env;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            User user = db.Users.FirstOrDefault(u => u.Login == User.Identity.Name);

            if (user != null)
            {
                switch (user.Role.Name)
                {
                    case "mounter":
                        Worker worker = db.Workers.Find(user.WorkerID);
                        ViewBag.Data = PrepareMounterData(worker);
                        return View(worker);
                    case "curator":
                        ViewBag.Data = PrepareCuratorData(user.Id);
                        ViewBag.Name = user.Name;
                        return View("~/Views/Home/CuratorIndex.cshtml");
                    case "engineer":
                        ViewBag.Data = PrepareEngineerData();
                        ViewBag.Name = user.Name;
                        return View("~/Views/Home/EngineerIndex.cshtml");
                    case "administrator":
                        ViewBag.Data = PrepareEngineerData();
                        ViewBag.Name = user.Name;
                        return View("~/Views/Home/EngineerIndex.cshtml");

                    default:
                        ViewData["error"] = "Не определенная роль пользователя";
                        return View("~/Views/Shared/_ErrorPage.cshtml");
                }
            }
            else return View("Login");

        }

        public async Task<IActionResult> Stat() //Страница статистики для монтажника 
        {
            if (User.IsInRole("mounter"))
            {
                return await MounterStat();
            }
            if (User.IsInRole("engineer") || User.IsInRole("curator"))
            {
                return EngineerStat();
            }
            return null;
        }

        public async Task<IActionResult> MounterStat(int workerId) //страница статистики по работнику для куратора 
        {
            ViewBag.reportType = "mounter";
            int inWork = 0;
            int totalCount = 0;
            int mounted = 0;
            int returned = 0;
            bool deliveryAvaliable = true;

            Worker worker = await db.Workers.FindAsync(workerId);


            if (worker != null)
            {
                MounterReportModel report = new MounterReportModel(worker.Id, worker.DeliveryAvailible);
                List<WorkerDevice> workerDevices = new List<WorkerDevice>();

                WorkerType workerType = await db.WorkerTypes.FindAsync(worker.WorkerTypeId);
                int n = worker.DeliveryActs.Count();
                if (!worker.DeliveryAvailible)
                {
                    ViewData["error"] = "Выдача заблокирована!";
                }

                WorkerManager workerManager = new WorkerManager(db);
                List<Device> devices = workerManager.GetWorkerDevices(worker);
                List<DeviceDelivery> mounterDeliveries = workerManager.GetDeviceDeliveries(worker);
                foreach (var deviceDelivery in mounterDeliveries)
                {
                    Device device = deviceDelivery.Device;
                    if (device != null && device.DeviceType.Type == "ПУ")
                    {
                        string RowColorCode = "";
                        TimeSpan span = DateTime.Now - deviceDelivery.DeliveryAct.Date;
                        if (span.Days > 10 && (device.CurrentState == "выдача со склада" || device.CurrentState == "включен в отчет"))
                        {
                            RowColorCode = "#fdc8c8";
                        }
                        else
                        {
                            RowColorCode = "#FFFFFF";
                        }
                        if (device.CurrentState == "выдача со склада" || device.CurrentState == "включен в отчет")
                        {
                            workerDevices.Add(new WorkerDevice
                            {
                                Serial = deviceDelivery.SerialNumber,
                                DeviceType = deviceDelivery.DeviceType.Name,
                                Date = deviceDelivery.DeliveryAct.Date,
                                RowColorCode = RowColorCode
                            });
                            inWork++;
                        }
                        if (device.CurrentState == "привязан к ту")
                        {
                            mounted++;
                        }

                        totalCount++;
                    }
                }
                returned = 0;
                inWork = 0;
                mounted = 0;
                totalCount = 0;
                foreach (var item in devices)
                {
                    if (item.CurrentState == "выдача со склада" || item.CurrentState == "включен в отчет") inWork++;
                    if (item.CurrentState == "возврат на склад") returned++;
                    if (item.CurrentState == "привязан к ту" || item.CurrentState == "принят куратором") mounted++;
                }

                //Материалы
                List<WorkerMaterial> workerMaterials = new List<WorkerMaterial>();
                bool newMaterial = true;
                foreach (var item in workerManager.GetWorkerDeliveryActs(worker))
                {
                    foreach (var delivery in item.MaterialDeliveries)
                    {
                        newMaterial = true;
                        foreach (var workerMaterial in workerMaterials)
                        {
                            if (delivery.Material.Id == workerMaterial.Id)
                            {
                                newMaterial = false;
                                if (item.DeliveryType.Description == "выдача со склада")
                                    workerMaterial.Volume += Math.Abs(delivery.Volume);
                                if (item.DeliveryType.Description == "возврат на склад")
                                    workerMaterial.Volume -= Math.Abs(delivery.Volume);
                                break;
                            }

                        }
                        if (newMaterial)
                        {
                            WorkerMaterial tmp = new WorkerMaterial { Name = delivery.MaterialType.Name, Unit = delivery.MaterialType.Unit.Name, Id = delivery.Material.Id };
                            if (item.DeliveryType.Description == "выдача со склада")
                                tmp.Volume += Math.Abs(delivery.Volume);
                            if (item.DeliveryType.Description == "возврат на склад")
                                tmp.Volume -= Math.Abs(delivery.Volume);
                            workerMaterials.Add(tmp);
                            newMaterial = false;
                        }

                    }
                }


                totalCount = devices.Count();

                ViewBag.inWork = inWork;
                ViewBag.mounted = mounted;
                ViewBag.totalCount = totalCount;
                report.WorkerDevices = workerDevices;
                report.WorkerMaterials = workerMaterials;

                return View("Stat", report);
            }
            else
            {
                ViewData["error"] = "Работник не найден";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<string> ChangeDeliveryState(int workerId, bool state) // Блокировка/разблокировка выдачи со склада
        {
            Worker worker = await db.Workers.FindAsync(workerId);
            if (worker != null)
            {
                worker.DeliveryAvailible = state;
                await db.SaveChangesAsync();
                if (state)
                    return "Выдача разрешена!";
                else return "Выдача заблокирована!";
            }
            else return "Ошибка изменения статуса, работник не найден!";
        }

        public IActionResult WorkerReportByDate(int workerId, string dates)
        {
            string[] strDates = dates.Split('-');

            if (DateTime.TryParse(strDates[0], out DateTime startDate) && DateTime.TryParse(strDates[1], out DateTime endDate))
            {
                endDate.AddHours(23);
                ReportGenerator reportGenerator = new ReportGenerator(db, _env);
                string filePath = reportGenerator.GenerateWorekerReportByDate(workerId, startDate, endDate);
                return PhysicalFile(filePath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Отчет по работнику.xlsx");
            }
            else
            {
                ViewData["error"] = "Ошибка генерации отчета";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> GetGraphData ()
        {
            var alReports = await db.MounterReportUgesALs.ToListAsync();
            var supports = await db.PowerLineSupports.ToListAsync();
            var kdes = await db.KDEs.ToListAsync();
            var pus = await db.MounterReportUgesALItems.ToListAsync();

            var sbReports = await db.SBReports.ToListAsync();
            var uspdReports = await db.USPDReports.ToListAsync();
            var switches = await db.Switches.ToListAsync();

            var deliveryActs = await db.DeliveryActs.Where(a => a.DeliveryTypeId == 4).ToListAsync();
            var deviceDeliveries = await db.DeviceDeliveries.ToListAsync();

            EngineerData engineerData = new EngineerData();
            //Статистика по ЛК (по контракту)
            var lkStat = new LkStat(1);
            WorkerManager workerManager = new WorkerManager(db);
            foreach (var user in db.Users.Where(u => u.Role.Name == "mounter")) //подсчет кол-ва пу по отчетам монтажников 
            {
                MounterPUCount mounterPUCount = new MounterPUCount();
                mounterPUCount.WorkerId = (int)user.WorkerID;
                Worker worker = db.Workers.Find(user.WorkerID);
                mounterPUCount.Name = workerManager.GetShortWorkerName(worker);
                foreach (var alReport in alReports.Where(r => r.WorkerId == worker.Id))
                {
                    foreach (var support in supports.Where(s => s.MounterReportUgesALId == alReport.Id))
                    {
                        foreach (var kde in kdes.Where(k => k.PowerLineSupportId == support.Id))
                        {
                            foreach (var pu in pus.Where(p => p.KDEId == kde.Id))
                            {
                                if (alReport.ReportStateId == 1 || alReport.ReportStateId == 4 || alReport.ReportStateId == 2)
                                    mounterPUCount.InWorkCount++;
                                if (alReport.ReportStateId == 3 || alReport.ReportStateId == 5)
                                    mounterPUCount.AcceptedCount++;
                                mounterPUCount.TotalCount++;

                            }
                        }
                    }
                }
                foreach (var sbReport in sbReports.Where(r => r.WorkerId == user.WorkerID))
                {
                    foreach (var pu in switches.Where(s => s.SBReportId == sbReport.Id))
                    {
                        if (sbReport.ReportStateId == 1 || sbReport.ReportStateId == 4 || sbReport.ReportStateId == 2)
                            mounterPUCount.InWorkCount++;
                        if (sbReport.ReportStateId == 3 || sbReport.ReportStateId == 5)
                            mounterPUCount.AcceptedCount++;
                        mounterPUCount.TotalCount++;
                    }
                }
                foreach (var uspdReport in uspdReports.Where(r => r.WorkerId == user.WorkerID))
                {
                    foreach (var pu in switches.Where(s => s.USPDReportId == uspdReport.Id))
                    {
                        if (uspdReport.ReportStateId == 1 || uspdReport.ReportStateId == 4 || uspdReport.ReportStateId == 2)
                            mounterPUCount.InWorkCount++;
                        if (uspdReport.ReportStateId == 3 || uspdReport.ReportStateId == 5)
                            mounterPUCount.AcceptedCount++;
                        mounterPUCount.TotalCount++;
                    }
                }
                mounterPUCount.Recived = workerManager.GetWorkerDevicesCount(worker.Id);
                lkStat.MounterPUCounts.Add(mounterPUCount);
            }

            ReportCount reportCount = new ReportCount();
            reportCount.Type = "ВЛ";
            foreach (var alReport in alReports)
            {
                if (alReport.ReportStateId == 1)
                    reportCount.InWorkCount++;
                if (alReport.ReportStateId == 4)
                    reportCount.RemarksCount++;
                if (alReport.ReportStateId == 3)
                    reportCount.AcceptedCount++;
                if (alReport.ReportStateId == 5)
                {
                    reportCount.AcceptedCount++;
                    reportCount.ImportedCount++;
                }
                if (alReport.ReportStateId == 2)
                    reportCount.SentCount++;
                reportCount.TotalCount++;
            }
            lkStat.ReportCounts.Add(reportCount);

            reportCount = new ReportCount();
            reportCount.Type = "ТП/РП";
            foreach (var sbreport in sbReports)
            {
                if (sbreport.ReportStateId == 1)
                    reportCount.InWorkCount++;
                if (sbreport.ReportStateId == 4)
                    reportCount.RemarksCount++;
                if (sbreport.ReportStateId == 3)
                    reportCount.AcceptedCount++;
                if (sbreport.ReportStateId == 5)
                {
                    reportCount.AcceptedCount++;
                    reportCount.ImportedCount++;
                }
                if (sbreport.ReportStateId == 2)
                    reportCount.SentCount++;
                reportCount.TotalCount++;
            }
            lkStat.ReportCounts.Add(reportCount);

            reportCount = new ReportCount();
            reportCount.Type = "УСПД";
            foreach (var uspdreport in uspdReports)
            {
                if (uspdreport.ReportStateId == 1)
                    reportCount.InWorkCount++;
                if (uspdreport.ReportStateId == 4)
                    reportCount.RemarksCount++;
                if (uspdreport.ReportStateId == 3)
                    reportCount.AcceptedCount++;
                if (uspdreport.ReportStateId == 5)
                {
                    reportCount.AcceptedCount++;
                    reportCount.ImportedCount++;
                }
                if (uspdreport.ReportStateId == 2)
                    reportCount.SentCount++;
                reportCount.TotalCount++;
            }
            lkStat.ReportCounts.Add(reportCount);

            engineerData.LkStat = lkStat;

            ViewBag.Data = engineerData;
            return PartialView("_graphs");

        }

        private async Task<IActionResult> MounterStat()//Статистика для монтажников
        {
            ViewBag.reportType = "mounter";
            int inWork = 0;
            int totalCount = 0;
            int mounted = 0;
            int returned = 0;


            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FindAsync(user.WorkerID);
            if (worker != null)
            {
                MounterReportModel report = new MounterReportModel(worker.Id, worker.DeliveryAvailible);
                List<WorkerDevice> workerDevices = new List<WorkerDevice>();

                WorkerType workerType = await db.WorkerTypes.FindAsync(worker.WorkerTypeId);
                int n = worker.DeliveryActs.Count();
                if (!worker.DeliveryAvailible)
                {
                    ViewData["error"] = "Выдача заблокирована!";
                }

                WorkerManager workerManager = new WorkerManager(db);
                List<Device> devices = workerManager.GetWorkerDevices(worker);
                List<DeviceDelivery> mounterDeliveries = workerManager.GetDeviceDeliveries(worker);
                foreach (var deviceDelivery in mounterDeliveries)
                {
                    Device device = deviceDelivery.Device;
                    if (device != null && device.DeviceType.Type == "ПУ")
                    {
                        string RowColorCode = "";
                        TimeSpan span = DateTime.Now - deviceDelivery.DeliveryAct.Date;
                        if (span.Days > 10 && (device.CurrentState == "выдача со склада" || device.CurrentState == "включен в отчет"))
                        {
                            RowColorCode = "#fdc8c8";
                        }
                        else
                        {
                            RowColorCode = "#FFFFFF";
                        }
                        if (device.CurrentState == "выдача со склада" || device.CurrentState == "включен в отчет")
                        {
                            workerDevices.Add(new WorkerDevice
                            {
                                Serial = deviceDelivery.SerialNumber,
                                DeviceType = deviceDelivery.DeviceType.Name,
                                Date = deviceDelivery.DeliveryAct.Date,
                                RowColorCode = RowColorCode
                            });
                            inWork++;
                        }
                        if (device.CurrentState == "привязан к ту")
                        {
                            mounted++;
                        }

                        totalCount++;
                    }
                }
                returned = 0;
                inWork = 0;
                mounted = 0;
                totalCount = 0;
                foreach (var item in devices)
                {
                    if (item.CurrentState == "выдача со склада" || item.CurrentState == "включен в отчет") inWork++;
                    if (item.CurrentState == "возврат на склад") returned++;
                    if (item.CurrentState == "привязан к ту" || item.CurrentState == "принят куратором") mounted++;
                }

                //Материалы
                List<WorkerMaterial> workerMaterials = new List<WorkerMaterial>();
                bool newMaterial = true;
                foreach (var item in workerManager.GetWorkerDeliveryActs(worker))
                {
                    foreach (var delivery in item.MaterialDeliveries)
                    {
                        newMaterial = true;
                        foreach (var workerMaterial in workerMaterials)
                        {
                            if (delivery.Material.Id == workerMaterial.Id)
                            {
                                newMaterial = false;
                                if (item.DeliveryType.Description == "выдача со склада")
                                    workerMaterial.Volume += Math.Abs(delivery.Volume);
                                if (item.DeliveryType.Description == "возврат на склад")
                                    workerMaterial.Volume -= Math.Abs(delivery.Volume);
                                break;
                            }

                        }
                        if (newMaterial)
                        {
                            WorkerMaterial tmp = new WorkerMaterial { Name = delivery.MaterialType.Name, Unit = delivery.MaterialType.Unit.Name, Id = delivery.Material.Id };
                            if (item.DeliveryType.Description == "выдача со склада")
                                tmp.Volume += Math.Abs(delivery.Volume);
                            if (item.DeliveryType.Description == "возврат на склад")
                                tmp.Volume -= Math.Abs(delivery.Volume);
                            workerMaterials.Add(tmp);
                            newMaterial = false;
                        }

                    }
                }


                totalCount = devices.Count();

                ViewBag.inWork = inWork;
                ViewBag.mounted = mounted;
                ViewBag.totalCount = totalCount;
                report.WorkerDevices = workerDevices;
                report.WorkerMaterials = workerMaterials;

                return View("Stat", report);
            }
            else
            {
                ViewData["error"] = "Работник не найден";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        private IActionResult EngineerStat()//Статистика по монтажникам
        {
            List<DropDownItem> dropDownMounters = new List<DropDownItem>();
            dropDownMounters.Add(new DropDownItem { Id = 0, Name = "---" });
            foreach (var item in db.Workers.Where(p => p.WorkerType.Description == "монтажник"))
            {
                dropDownMounters.Add(new DropDownItem { Id = item.Id, Name = item.Surname + " " + item.Name + " " + item.MIddlename });
            }
            SelectList mounters = new SelectList(dropDownMounters, "Id", "Name");
            ViewBag.Mounters = mounters;
            return View("EngineerStat");
        }

        private List<MounterData> PrepareMounterData(Worker worker) //данные для страницы монтажника
        {
            List<MounterData> data = new List<MounterData>();
            int recived = 0;
            int acepted = 0;
            foreach (var contract in db.Contracts)
            {
                foreach (var act in contract.DeliveryActs)
                {
                    if (act.Worker == worker)
                    {
                        foreach (var deviceDelivery in act.DeviceDeliveries)
                        {
                            if (deviceDelivery.Device.DeviceType.Type == "ПУ" || deviceDelivery.Device.DeviceType.Type == "УСПД")
                            {
                                string currentState = deviceDelivery.Device.CurrentState;
                                //if (currentState == "выдача со склада" || currentState == "включен в отчет")
                                // {
                                recived++;
                                //}
                                if (currentState == "принят куратором" || currentState == "привязан к ту")
                                {
                                    acepted++;
                                }
                            }
                           
                        }
                    }
                }
                if (recived > 0 || acepted > 0)
                {
                    data.Add(new Models.MounterData { ContractName = contract.Name, Accepted = acepted, Recived = recived, ContractId = contract.Id });
                    recived = 0;
                    acepted = 0;
                }
            }
            return data;
        }

        private List<CuratorData> PrepareCuratorData(int curatorId) //данные для страницы куратора 
        {
            WorkerManager workerManager = new WorkerManager(db);
            List<CuratorData> data = new List<CuratorData>();
            ReportState sendedReport = db.ReportStates.FirstOrDefault(r => r.Description == "отправлен куратору");
            ReportState acceptedReport = db.ReportStates.FirstOrDefault(r => r.Description == "принят куратором");
            ReportState importedReport = db.ReportStates.FirstOrDefault(r => r.Description == "импортирован");
            ReportState forImoirtReport = db.ReportStates.FirstOrDefault(r => r.Description == ReportStateTypeName.ForImport.ToString());
            bool newMounter = false;
            foreach (var worker in db.Workers.Where(u => u.WorkerType.Description == "монтажник"))
            {
                foreach (var report in db.MounterReportUgesALs.Where(r => r.WorkerId == worker.Id && r.CuratorId == curatorId && (r.ReportStateId == sendedReport.Id || r.ReportStateId == acceptedReport.Id || r.ReportStateId == importedReport.Id || r.ReportStateId == forImoirtReport.Id)))
                {
                    if (data.Count > 0)
                    {
                        foreach (var mounter in data)
                        {
                            if (mounter.MounterName == workerManager.GetWorkerName(worker) && (report.ReportStateId == sendedReport.Id || report.ReportStateId == acceptedReport.Id || report.ReportStateId == importedReport.Id || report.ReportStateId == forImoirtReport.Id))
                                newMounter = false;

                            else newMounter = true;
                        }
                    }
                    else data.Add(new CuratorData { MounterName = workerManager.GetWorkerName(worker), Recived = 0, Accepted = 0, MounterId = worker.Id });

                    if (newMounter) data.Add(new CuratorData { MounterName = workerManager.GetWorkerName(worker), Recived = 0, Accepted = 0, MounterId = worker.Id });
                }

                foreach (var report in db.SBReports.Where(r => r.WorkerId == worker.Id && r.CuratorId == curatorId && (r.ReportStateId == sendedReport.Id || r.ReportStateId == acceptedReport.Id || r.ReportStateId == importedReport.Id || r.ReportStateId == forImoirtReport.Id)))
                {
                    if ((report.ReportStateId == sendedReport.Id || report.ReportStateId == acceptedReport.Id || report.ReportStateId == importedReport.Id || report.ReportStateId == forImoirtReport.Id) && report.CuratorId == curatorId)
                    {
                        if (data.Count > 0)
                        {
                            foreach (var mounter in data)
                            {
                                if (mounter.MounterName == workerManager.GetWorkerName(worker) && (report.ReportStateId == sendedReport.Id || report.ReportStateId == acceptedReport.Id || report.ReportStateId == importedReport.Id || report.ReportStateId == forImoirtReport.Id))
                                    newMounter = false;

                                else newMounter = true;
                            }
                        }
                        else data.Add(new CuratorData { MounterName = workerManager.GetWorkerName(worker), Recived = 0, Accepted = 0, MounterId = worker.Id });

                        if (newMounter) data.Add(new CuratorData { MounterName = workerManager.GetWorkerName(worker), Recived = 0, Accepted = 0, MounterId = worker.Id });
                    }
                }

                foreach (var report in db.USPDReports.Where(r => r.WorkerId == worker.Id && r.CuratorId == curatorId && (r.ReportStateId == sendedReport.Id || r.ReportStateId == acceptedReport.Id || r.ReportStateId == importedReport.Id || r.ReportStateId == forImoirtReport.Id)))
                {
                    if ((report.ReportStateId == sendedReport.Id || report.ReportStateId == acceptedReport.Id || report.ReportStateId == importedReport.Id || report.ReportStateId == forImoirtReport.Id) && report.CuratorId == curatorId)
                    {
                        if (data.Count > 0)
                        {
                            foreach (var mounter in data)
                            {
                                if (mounter.MounterName == workerManager.GetWorkerName(worker) && (report.ReportStateId == sendedReport.Id || report.ReportStateId == acceptedReport.Id || report.ReportStateId == importedReport.Id || report.ReportStateId == forImoirtReport.Id))
                                    newMounter = false;
                                else newMounter = true;
                            }
                        }
                        else data.Add(new CuratorData { MounterName = workerManager.GetWorkerName(worker), Recived = 0, Accepted = 0, MounterId = worker.Id });

                        if (newMounter) data.Add(new CuratorData { MounterName = workerManager.GetWorkerName(worker), Recived = 0, Accepted = 0, MounterId = worker.Id });
                    }
                }

                foreach (var report in db.UnmountReports.Where(r => r.WorkerId == worker.Id && r.CuratorId == curatorId && (r.ReportStateId == sendedReport.Id || r.ReportStateId == acceptedReport.Id || r.ReportStateId == importedReport.Id || r.ReportStateId == forImoirtReport.Id)))
                {
                    if ((report.ReportStateId == sendedReport.Id || report.ReportStateId == acceptedReport.Id || report.ReportStateId == importedReport.Id || report.ReportStateId == forImoirtReport.Id) && report.CuratorId == curatorId)
                    {
                        if (data.Count > 0)
                        {
                            foreach (var mounter in data)
                            {
                                if (mounter.MounterName == workerManager.GetWorkerName(worker) && (report.ReportStateId == sendedReport.Id || report.ReportStateId == acceptedReport.Id || report.ReportStateId == importedReport.Id || report.ReportStateId == forImoirtReport.Id))
                                    newMounter = false;
                                else newMounter = true;
                            }
                        }
                        else data.Add(new CuratorData { MounterName = workerManager.GetWorkerName(worker), Recived = 0, Accepted = 0, MounterId = worker.Id });

                        if (newMounter) data.Add(new CuratorData { MounterName = workerManager.GetWorkerName(worker), Recived = 0, Accepted = 0, MounterId = worker.Id });
                    }
                }
            }
            foreach (var mounter in data)
            {
                foreach (var report in db.MounterReportUgesALs.Where(r => r.WorkerId == mounter.MounterId && r.CuratorId == curatorId))
                {
                    if (report.ReportState.Description == "принят куратором" || report.ReportState.Description == "импортирован")
                        mounter.Accepted++;
                    if (report.ReportState.Description == "отправлен куратору")
                        mounter.Recived++;

                }
                foreach (var report in db.SBReports.Where(r => r.WorkerId == mounter.MounterId && r.CuratorId == curatorId))
                {
                    if (report.ReportState.Description == "принят куратором" || report.ReportState.Description == "импортирован")
                        mounter.Accepted++;
                    if (report.ReportState.Description == "отправлен куратору")
                        mounter.Recived++;
                }
                foreach (var report in db.USPDReports.Where(r => r.WorkerId == mounter.MounterId && r.CuratorId == curatorId))
                {
                    if (report.ReportState.Description == "принят куратором" || report.ReportState.Description == "импортирован")
                        mounter.Accepted++;
                    if (report.ReportState.Description == "отправлен куратору")
                        mounter.Recived++;
                }
                foreach (var report in db.UnmountReports.Where(r => r.WorkerId == mounter.MounterId && r.CuratorId == curatorId))
                {
                    if (report.ReportState.Description == "принят куратором" || report.ReportState.Description == "импортирован")
                        mounter.Accepted++;
                    if (report.ReportState.Description == "отправлен куратору")
                        mounter.Recived++;
                }
            }

            return data;
        }

        private EngineerData PrepareEngineerData() 
        {
            EngineerData engineerData = new EngineerData();
            //Статистика по кураторам
            List<CuratorCount> curatorCounts = new List<CuratorCount>();
            foreach (var curator in db.Users.Where(u => u.Role.Name == "curator"))
            {
                List<CuratorData> curatorData = PrepareCuratorData(curator.Id);
                int accepted = 0;
                int recived = 0;
                foreach (var mounter in curatorData)
                {
                    accepted += mounter.Accepted;
                    recived += mounter.Recived;
                }
                curatorCounts.Add(new CuratorCount { CuratorName = curator.Name, CuratorId = curator.Id, Accepted = accepted, Recived = recived });
            }
            engineerData.CuratorCounts = curatorCounts;

            return engineerData;
        } //данные для страницы инженера

   
    }
}
