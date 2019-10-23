using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbManager;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewMounterAccount.Models;

namespace NewMounterAccount.Controllers
{
    public class CuratorController : Controller
    {
        StoreContext db;
        readonly IHostingEnvironment _env;
        public CuratorController(StoreContext context, IHostingEnvironment env)
        {
            db = context;
            _env = env;
        }
        public async Task<IActionResult> Reports (int Id)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            Worker worker = await db.Workers.FindAsync(Id);
            WorkerManager workerManager = new WorkerManager(db);

            //Списки отчетов по ВЛ
            var acceptedAlReports = await ALReportData(Id, user.Id, "принят куратором");
            var importedAlReports = await ALReportData(Id, user.Id, "импортирован");
            var forImportAlReports = await ALReportData(Id, user.Id, ReportStateTypeName.ForImport.ToString());
            ViewBag.ALReports = await ALReportData(Id, user.Id, "отправлен куратору");
            ViewBag.AcceptedALReports = acceptedAlReports.Concat(importedAlReports).ToList();
           // ViewBag.AcceptedALReports = acceptedAlReports.Concat(forImportAlReports).ToList();


            //Списки отчетов по ТП/РП
            var acceptedSBReports = await SBReportData(Id, user.Id, "принят куратором");
            var importedSBReports = await SBReportData(Id, user.Id, "импортирован");
            var forImportSBReports = await SBReportData(Id, user.Id, ReportStateTypeName.ForImport.ToString());
            ViewBag.SBReports = await SBReportData(Id, user.Id, "отправлен куратору");
            ViewBag.AcceptedSBReports = acceptedSBReports.Concat(importedSBReports).ToList();
            //ViewBag.AcceptedSBReports = acceptedSBReports.Concat(forImportSBReports).ToList();

            //Списки отчетов по УСПД
            var acceptedUSPDReports = await USPDReportData(Id, user.Id, "принят куратором");
            var importedUSPDReports = await USPDReportData(Id, user.Id, "импортирован");
            var forImportUSPDReports = await USPDReportData(Id, user.Id, ReportStateTypeName.ForImport.ToString());
            ViewBag.USPDReports = await USPDReportData(Id, user.Id, "отправлен куратору");
            ViewBag.AcceptedUSPDReports = acceptedUSPDReports.Concat(importedUSPDReports).ToList();

            //Списки отчетов по Демонтажу
            var acceptedUnmountReports = await UnmountReportData(Id, user.Id, "принят куратором");
            var importedUnmountReports = await UnmountReportData(Id, user.Id, "импортирован");
            var forImporUnmountReports = await UnmountReportData(Id, user.Id, ReportStateTypeName.ForImport.ToString());
            ViewBag.UnmountReports = await UnmountReportData(Id, user.Id, "отправлен куратору");
            ViewBag.AcceptedUnmountReports = acceptedUnmountReports.Concat(importedUnmountReports).ToList();
            //ViewBag.AcceptedUSPDReports = acceptedUSPDReports.Concat(importedUSPDReports).ToList();
            ViewBag.WorkerName = workerManager.GetWorkerName(worker);
            return View();
        }

        #region Операции с отчетами

        public async Task<IActionResult> AcceptReport (string type, int id)
        {
            switch(type)
            {
                case "ВЛ":
                    return await AcceptALReport(id);
                   
                case "ТП/РП":
                    return await AcceptSBReport(id);
                    
                case "УСПД":
                   return await AccceptUSPDReport(id);

                case "Демонтаж":
                    return await AcceptUnmountReport(id);
                default:
                    ViewData["error"] = "Ошибка открытия отчета!";
                    return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        private async Task<IActionResult> AcceptALReport(int reportId)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(reportId);
            ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(r => r.Description == "принят куратором");
            
            foreach(var support in report.PowerLineSupports)
            {
                foreach (var kde in support.KDEs)
                {
                    foreach (var device in kde.MounterReportUgesDeviceItems)
                    {
                        Device pu = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == device.Serial);
                        if (pu.CurrentState != "включен в отчет" && pu.CurrentState != "принят куратором")
                        {
                            ViewData["error"] = "Ошибка статуса ПУ №" + device.Serial + "!";
                            return View("~/Views/Shared/_ErrorPage.cshtml");
                        }
                        else
                        {
                            pu.CurrentState = "принят куратором";
                            await AddDeviceState(pu.Id, user.Id, "принят куратором");
                        }
                    }
                }
            }
            report.ReportStateId = reportState.Id;
            await db.SaveChangesAsync();
            return Redirect("~/Curator/Reports/" + report.WorkerId);
        }

        private async Task<IActionResult> AcceptSBReport (int reportId)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            SBReport report = await db.SBReports.FindAsync(reportId);
            ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(r => r.Description == "принят куратором");

            foreach (var device in report.Switches)
            {
                Device pu = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == device.DeviceSerial);
                if (pu.CurrentState != "включен в отчет" && pu.CurrentState != "принят куратором")
                {
                    ViewData["error"] = "Ошибка статуса ПУ №" + device.DeviceSerial + "!";
                    return View("~/Views/Shared/_ErrorPage.cshtml");
                }
                else
                {
                    pu.CurrentState = "принят куратором";
                    await AddDeviceState(pu.Id, user.Id, "принят куратором");
                }
            }

            report.ReportStateId = reportState.Id;
            await db.SaveChangesAsync();
            return Redirect("~/Curator/Reports/" + report.WorkerId);
        }

        private async Task<IActionResult> AccceptUSPDReport (int reportId)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            USPDReport report = await db.USPDReports.FindAsync(reportId);
            ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(r => r.Description == "принят куратором");

            foreach (var device in report.Switches)
            {
                Device pu = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == device.DeviceSerial);
                if (pu.CurrentState != "включен в отчет" && pu.CurrentState != "принят куратором")
                {
                    ViewData["error"] = "Ошибка статуса ПУ №" + device.DeviceSerial + "!";
                    return View("~/Views/Shared/_ErrorPage.cshtml");
                }
                else
                {
                    pu.CurrentState = "принят куратором";
                    await AddDeviceState(pu.Id, user.Id, "принят куратором");
                }
            }
            report.ReportStateId = reportState.Id;
            Device uspd = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == report.UspdSerial);
            Device plc = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == report.PlcSerial);
            uspd.AddState(DeviceStateTypeName.AcceptedByCurator, user.Id, null);
            plc.AddState(DeviceStateTypeName.AcceptedByCurator, user.Id, null);
            
            await db.SaveChangesAsync();
            return Redirect("~/Curator/Reports/" + report.WorkerId);
        }

        private async Task<IActionResult> AcceptUnmountReport (int reportId)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            UnmountReport report = await db.UnmountReports.FindAsync(reportId);
            ReportState reportState = await db.ReportStates.FirstOrDefaultAsync(r => r.Description == ReportStateTypeName.ForImport.ToString());

            report.ReportStateId = reportState.Id;
            await db.SaveChangesAsync();
            return Redirect("~/Curator/Reports/" + report.WorkerId);
        }

        public async Task<string> AddRemarkToReport(int reportId,  string reportType, string text)
        {
            User user = await db.Users.FirstOrDefaultAsync(u => u.Login == User.Identity.Name);
            switch (reportType)
            {
                case "ВЛ":
                    MounterReportUgesAL report = await db.MounterReportUgesALs.FindAsync(reportId);
                    ReportType reportTp = await db.ReportTypes.Where(t => t.Description == reportType).FirstAsync();
                    if (report != null && reportTp != null)
                    {
                        await db.ReportRemarks.AddAsync(new ReportRemark { Text = text, ReportId = reportId, ReportTypeId = reportTp.Id, UserId = user.Id });
                        ReportState state = await db.ReportStates.Where(s => s.Description == "с замечаниями куратора").FirstAsync();
                        report.ReportStateId = state.Id;
                        await db.SaveChangesAsync();
                        return "Замечания отправлены!";
                    }
                    else return "Ошибка сохранения отчета!";

                case"ТП/РП":
                    SBReport sbReport = await db.SBReports.FindAsync(reportId);
                    ReportType sbReportTp = await db.ReportTypes.Where(t => t.Description == reportType).FirstAsync();
                    if (sbReport != null && sbReportTp != null)
                    {
                        await db.ReportRemarks.AddAsync(new ReportRemark { Text = text, ReportId = reportId, ReportTypeId = sbReportTp.Id, UserId = user.Id });
                        ReportState state = await db.ReportStates.Where(s => s.Description == "с замечаниями куратора").FirstAsync();
                        sbReport.ReportStateId = state.Id;
                        await db.SaveChangesAsync();
                        return "Замечания отправлены!";
                    }
                    else return "Ошибка сохранения отчета!";

                case "УСПД":
                    USPDReport uspdReport = await db.USPDReports.FindAsync(reportId);
                    ReportType uspdReportTp = await db.ReportTypes.Where(t => t.Description == reportType).FirstAsync();
                    if (uspdReport != null && uspdReportTp != null)
                    {
                        await db.ReportRemarks.AddAsync(new ReportRemark { Text = text, ReportId = reportId, ReportTypeId = uspdReportTp.Id, UserId = user.Id });
                        ReportState state = await db.ReportStates.Where(s => s.Description == "с замечаниями куратора").FirstAsync();
                        uspdReport.ReportStateId = state.Id;
                        await db.SaveChangesAsync();
                        return "Замечания отправлены!";
                    }
                    else return "Ошибка сохранения отчета!";

                case "Демонтаж":
                    UnmountReport unmountReport = await db.UnmountReports.FindAsync(reportId);
                    ReportType unmountReportType = await db.ReportTypes.Where(t => t.Description == reportType).FirstAsync();
                    if (unmountReport != null && unmountReportType != null)
                    {
                        await db.ReportRemarks.AddAsync(new ReportRemark { Text = text, ReportId = reportId, ReportTypeId = unmountReportType.Id, UserId = user.Id });
                        ReportState state = await db.ReportStates.Where(s => s.Description == "с замечаниями куратора").FirstAsync();
                        unmountReport.ReportStateId = state.Id;
                        await db.SaveChangesAsync();
                        return "Замечания отправлены!";
                    }
                    else return "Ошибка сохранения отчета!";

                default:
                   return "Операция не определена!";
            }
      
        }
        #endregion

        private async Task<List<ReportItem>> ALReportData(int workerId, int curatotId, string state)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.MounterReportUgesALs.Where(r => r.WorkerId == workerId && r.CuratorId == curatotId && r.ReportState.Description == state))
            {
                mounted = 0;
                accepted = 0;
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

        private async Task<List<ReportItem>> SBReportData(int workerId, int curatorId, string state)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.SBReports.Where(r => r.WorkerId == workerId && r.CuratorId == curatorId && r.ReportState.Description == state))
            {
                mounted = item.Switches.Count;
                reports.Add(new ReportItem { Substation = item.Substation, Id = item.Id, Mounted = mounted, Accepted = accepted, NetRegion = item.NetRegion.Name, Date = item.Date, Type = "ТП/РП", State = item.ReportState.Description });
            }
            return reports;
        }

        private async Task<List<ReportItem>> USPDReportData(int workerId, int curatorId, string state)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.USPDReports.Where(r => r.WorkerId == workerId && r.CuratorId == curatorId && r.ReportState.Description == state))
            {
                mounted = item.Switches.Count;
                reports.Add(new ReportItem { Substation = item.Substation, Id = item.Id, Mounted = mounted, Accepted = accepted, NetRegion = item.NetRegion.Name, Date = item.Date, Type = "УСПД", State = item.ReportState.Description });
            }
            return reports;
        }

        private async Task<List<ReportItem>> UnmountReportData(int workerId, int curatorId, string state)
        {
            List<ReportItem> reports = new List<ReportItem>();
            int mounted = 0;
            int accepted = 0;
            foreach (var item in db.UnmountReports.Where(r => r.WorkerId == workerId && r.CuratorId == curatorId && r.ReportState.Description == state))
            {
                mounted = item.UnmountedDevices.Count();
                reports.Add(new ReportItem {  Id = item.Id, Mounted = mounted, Accepted = accepted,  Date = item.Date, Type = "Демонтаж", State = item.ReportState.Description, NetRegion = "-", Substation ="-" });
            }
            return reports;
        }

        private async Task AddDeviceState (int deviceId, int userId, string state)
        {
            DeviceStateType stateType = await db.DeviceStateTypes.FirstOrDefaultAsync(s => s.Description == state);
            await db.DeviceStates.AddAsync(new DeviceState { DeviceId = deviceId, UserId = userId, DeviceStateTypeId = stateType.Id, Date = DateTime.Now});
            await db.SaveChangesAsync();
        }
    }
}