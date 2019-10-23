using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DbManager;
using Microsoft.AspNetCore.Hosting;
using NewMounterAccount.Models;

namespace NewMounterAccount.AppCode
{
    public class ReportGenerator
    {
        StoreContext _db;
        IHostingEnvironment _env;

        public ReportGenerator(StoreContext context, IHostingEnvironment env)
        {
            _db = context;
            _env = env;
        }

        public string GenerateAlReport(int id)
        {
            XLWorkbook wb = null;
            IXLWorksheet ws = null;
            try
            {
                wb = new XLWorkbook(_env.WebRootPath + "/Files/Templates/ALTemplate.xlsx");
                ws = wb.Worksheet(1);
            }
            catch
            {
                return "Ошибка открытия шаблона";
            }
            int row = 7;
            int page = 1; 
            MounterReportUgesAL report = _db.MounterReportUgesALs.Find(id);
            if (report != null)
            {
                List<AdditionalMaterialModel> additionalMaterials = new List<AdditionalMaterialModel>();

                ws.Cell("C1").Value = report.Date.ToShortDateString();
                ws.Cell("C2").Value = report.Substation;
                ws.Cell("C3").Value = report.WorkPermit;
                ws.Cell("I1").Value = report.Local;
                ws.Cell("I2").Value = report.Fider;
                ws.Cell("I3").Value = report.Brigade.Replace('$', ' ');
                ws.Cell("R1").Value = report.NetRegion.Name + " ПО УГЭС";

                int count = 1;
                int printCount = 1;
                int kdeDeviceCount = 0;
                foreach (var support in report.PowerLineSupports)
                {
                    int deviceCount = 0;
                    int startRow = row;

                    foreach (var kde in support.KDEs)
                    {
                        foreach (var device in kde.MounterReportUgesDeviceItems)
                            deviceCount++;
                    }

                    if (count + deviceCount > 9)
                    {
                        row = row + ((8 - count) * 3);
                        ALNextPage(ws, row);
                        page++;
                        count = 1;
                        row += 15;
                        startRow = row;
                    }

                    foreach (var kde in support.KDEs)
                    {
                        kdeDeviceCount = 0;
                        foreach (var device in kde.MounterReportUgesDeviceItems)
                        {
                            Device _device = _db.Devices.FirstOrDefault(d => d.SerialNumber == device.Serial);
                            foreach(var additionalMaterial in _db.AdditionalMaterials.Where(m => m.DeviceId == _device.Id))
                            {
                                additionalMaterials.Add(new AdditionalMaterialModel { Name = additionalMaterial.Material.MaterialType.Name, Unit = additionalMaterial.Material.MaterialType.Unit.Name, Volume = additionalMaterial.Volume });
                            }
                            if (_device != null)
                            {
                                kdeDeviceCount++;
                                ws.Range("A" + row, "A" + (row + 2)).Merge();
                                ws.Cell("A" + row).Value = printCount;

                                ws.Range("B" + row, "B" + (row + 2)).Merge();
                                ws.Range("C" + row, "C" + (row + 2)).Merge();
                                ws.Range("D" + row, "D" + (row + 2)).Merge();
                                ws.Range("E" + row, "E" + (row + 2)).Merge();
                                ws.Cell("B" + row).Value = device.Street;
                                ws.Cell("C" + row).Value = "'" + device.House;
                                ws.Cell("D" + row).Value = "'" + device.Building;
                                ws.Cell("E" + row).Value = "'" + device.Flat;

                                ws.Range("F" + row, "F" + (row + 2)).Merge();
                                ws.Cell("F" + row).Value = device.InstallPlace.First();

                                ws.Range("H" + row, "J" + row).Merge();
                                ws.Range("H" + (row + 1), "J" + (row + 1)).Merge();
                                ws.Range("H" + (row + 2), "J" + (row + 2)).Merge();
                                ws.Cell("H" + row).Value = _device.DeviceType.Name;
                                ws.Cell("H" + row).Style.Font.FontSize = 7;
                                ws.Cell("H" + (row + 1)).Value = "'" + _device.SerialNumber;
                                ws.Cell("H" + (row + 2)).Value = "'" + device.DeviceSeal;
                                if (device.PhoneNumber != null && device.PhoneNumber != "")
                                {
                                    ws.Cell("H" + (row + 2)).Style.Alignment.WrapText = true;
                                    ws.Cell("H" + (row + 2)).Value += "/" + device.PhoneNumber;
                                    ws.Cell("H" + (row + 2)).Style.Font.FontSize = 9;
                                }

                                ws.Cell("K" + row).Value = "∑:" + device.Sum;
                                ws.Cell("K" + (row + 1)).Value = "T1:" + device.T1;
                                ws.Cell("K" + (row + 2)).Value = "T2:" + device.T2;

                                ws.Cell("L" + row).Value = "U1:" + device.U1;
                                ws.Cell("L" + (row + 1)).Value = "U2:" + device.U2;
                                ws.Cell("L" + (row + 2)).Value = "U3:" + device.U3;

                                ws.Range("M" + row, "M" + (row + 2)).Merge();
                                ws.Cell("M" + row).Value = device.WireConsumptionUpDown;

                                ws.Range("N" + row, "N" + (row + 2)).Merge();
                                ws.Cell("N" + row).Value = device.WireConsumptionNewInput;

                                ws.Range("P" + row, "P" + (row + 2)).Merge();
                                ws.Cell("P" + row).Style.Alignment.WrapText = true;
                                ws.Cell("P" + row).Value = kde.KDEType.Name;

                                ws.Range("Q" + row, "Q" + (row + 2)).Merge();
                                ws.Cell("Q" + row).Value = "КДЕ:" + device.KDESeal + Environment.NewLine + "АВТ:" + device.SwitchSeal;
                                ws.Cell("Q" + row).Style.Font.FontSize = 8;

                                ws.Range("R" + row, "R" + (row + 2)).Merge();
                                ws.Cell("R" + row).Value = device.PowerLineType;

                                ws.Range("S" + row, "S" + (row + 2)).Merge();

                                ws.Range("A" + row, "S" + (row + 2)).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                ws.Range("A" + row, "S" + (row + 2)).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                ws.Range("A" + row, "S" + (row + 2)).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                ws.Range("A" + row, "S" + (row + 2)).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                                if (kde.KDEType.Name == "КДЕ-3-2" && kde.MounterReportUgesDeviceItems.Count == 2 && kdeDeviceCount == 1)
                                {
                                    row += 3;
                                    count++;
                                    printCount++;
                                }

                                if (kdeDeviceCount == 2 && kde.KDEType.Name == "КДЕ-3-2") //Объеденить ячейки с типом кде если кде-3-2 и в нем 2 ПУ
                                {
                                    ws.Range("P" + (row - 3), "P" + (row + 2)).Merge();
                                }
                            }
                        }

                        if (kde != support.KDEs.ToList().Last())
                        {
                            count++;
                            printCount++;
                            if (count > 8)
                            {
                                ALNextPage(ws, row);
                                page++;
                                count = 1;
                                row += 15;
                            }
                            else row += 3;
                            //ws.Row(row).InsertRowsBelow(3);
                        }
                        else
                        {
                            ws.Range("G" + startRow, "G" + (row + 2)).Merge();
                            ws.Cell("G" + startRow).Value = support.SupportNumber;

                            ws.Range("O" + startRow, "O" + (row + 2)).Merge();
                            ws.Cell("O" + startRow).Value = support.FixatorsCount;
                            if (support != report.PowerLineSupports.Last())
                            {
                                count++;
                                printCount++;
                                if (count > 8)
                                {
                                    ALNextPage(ws, row);
                                    page++;
                                    count = 1;
                                    row += 15;
                                }
                                else row += 3;
                            }
                        }
                    }
                }

                if(additionalMaterials.Count > 0) //Если были доп материалы 
                {
                    row = page * 37;
                    ws.Range("A1", "S3").CopyTo(ws.Cell("A" + (row)));

                    row += 4;
                    ws.Range("A" + row, "K" +row ).Merge();
                    ws.Cell("A" + row).Value = "Перечень доп. материалов";
                    row++;
                    int tableStartRow = row;
                        
                    ws.Range("A" + row, "G" + row).Merge();
                    ws.Cell("A"+row).Value = "Наименование материала";
                        
                    ws.Range("H" + row, "J" + row).Merge(); 
                    ws.Cell("H" + row).Value = "Ед. изм.";

                    ws.Cell("K" + row).Value = "Кол-во";

                    var range = ws.Range("A" + (row-1), "K" + row);
                    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Font.FontSize = 10;

                    row++;

                    foreach (var materilal in additionalMaterials)
                    {
                        ws.Range("A" + row, "G" + row).Merge();
                        ws.Cell("A"+row).Value = materilal.Name;
                        
                        ws.Range("H" + row, "J" + row).Merge(); 
                        ws.Cell("H" + row).Value = materilal.Unit;

                        ws.Cell("K" + row).Value = materilal.Volume;

                        range = ws.Range("A" + row, "K" + row);
                        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Font.FontSize = 10;

                        row++;
                    }
                }

               List<ReportComment> comments = _db.ReportComments.Where(r => r.ReportId == report.Id && r.ReportType.Description == "ВЛ").ToList();
                if(comments.Count > 0)
                {
                    row = page * 37;
                    row += 4;
                    ws.Range("L" + row, "S" + row).Merge();
                    ws.Cell("L" + row).Value = "Комметарии к отчету";
                    var range = ws.Range("L" + row, "S" + row);
                    range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    range.Style.Font.FontSize = 10;
                    row++;
                    
                    foreach(var comment in comments)
                    {
                        range = ws.Range("L" + row, "S" + (row + 1));
                        range.Merge();
                        ws.Cell("L" + row).Value = comment.Text + " от: " + comment.User.Name;
                        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Alignment.WrapText = true;
                        range.Style.Font.FontSize = 10;
                        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                        row += 2;
                    }

                }
            }

            WorkerManager workerManager = new WorkerManager(_db);
            //ws.Cell("C" + (row + 7)).Value = workerManager.GetShortWorkerName(report.Worker);
            string filePath = _env.WebRootPath + "/Files/Отчет_ВЛ_" + id + ".xlsx";
            wb.SaveAs(filePath);
            return filePath; //"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        }

        public string GenerateSBReport(int id) // генерация отчета по ТП/РП 
        {
            XLWorkbook wb = null;
            IXLWorksheet ws = null;
            try
            {
                wb = new XLWorkbook(_env.WebRootPath + "/Files/Templates/SBTemplate.xlsx");
                ws = wb.Worksheet(1);
            }
            catch
            {
                return "Ошибка открытия шаблона";
            }

            SBReport report = _db.SBReports.Find(id);
            if (report != null)
            {
                ws.Cell("A1").Value = report.Substation;
                ws.Cell("D1").Value = report.PhoneNumber;
                ws.Cell("K1").Value = "ПО УГЭС " + report.NetRegion.Name;
                ws.Cell("B2").Value = report.Date.ToShortDateString();
                ws.Cell("B3").Value = report.Phase;
                ws.Cell("D2").Value = report.WorkPermit;
                ws.Cell("D3").Value = report.Brigade;
             
                ws.Cell("B4").Value = report.MeterBoard;

                int startRow = 7;
                int row = 7;
                int count = 1;
                int page = 1; //Страницы отчета (36 строк) (1 сторка страницы = ((страница - 1 ) * 37 ) - 1)
                double kvvgVolume = 0;
                foreach (var @switch in report.Switches)
                {
                    SBswitchStyle(ws.Range("A" + row, "A" + (row + 2)));
                    ws.Cell("A" + row).Value = @switch.SwitchType + " №" + @switch.SwitchNumber;

                    SBswitchStyle(ws.Range("B" + row, "C" + (row + 2)));
                    ws.Cell("B" + row).Value = "'" + @switch.DeviceSerial;

                    SBswitchStyle(ws.Range("D" + row, "D" + (row + 2)));
                    ws.Cell("D" + row).Value ="'" + @switch.DeviceSeal;

                    SBswitchStyle(ws.Range("E" + row, "E" + (row + 2)));
                    ws.Cell("E" + row).Value = "'" + @switch.TestBoxSeal;

                    SBswitchStyle(ws.Range("F" + row, "G" + (row + 2)));
                    ws.Cell("F" + row).Value = @switch.TTAk + "/5" + Environment.NewLine + "№" + @switch.TTANumber + Environment.NewLine + "Пл:" + @switch.TTASeal;

                    SBswitchStyle(ws.Range("H" + row, "I" + (row + 2)));
                    ws.Cell("H" + row).Value = @switch.TTBk + "/5" + Environment.NewLine + "№" + @switch.TTBNumber + Environment.NewLine + "Пл:" + @switch.TTBSeal;

                    SBswitchStyle(ws.Range("J" + row, "K" + (row + 2)));
                    ws.Cell("J" + row).Value = @switch.TTCk + "/5" + Environment.NewLine + "№" + @switch.TTCNumber + Environment.NewLine + "Пл:" + @switch.TTCSeal;

                    SBswitchStyle(ws.Range("L" + row, "L" + (row + 2)));
                    ws.Cell("L" + row).Value = "∑= " + @switch.Sum + Environment.NewLine + "T1= " + @switch.T1 + Environment.NewLine + "T2= " + @switch.T2;

                    SBswitchStyle(ws.Range("M" + row, "M" + (row + 2)));
                    ws.Cell("M" + row).Value = @switch.KVVG.ToString("0.##");


                    count++;

                    if (count > 6)
                    {
                        if (@switch != report.Switches.Last())
                        {
                            SBNextPage(ws, row);
                            count = 1;
                            row += 23;
                        }
                        else row += 3;
                    }
                    else row += 3;


                }
                
                string filePath = _env.WebRootPath + "/Files/Отчет_ТПРП_" + id + ".xlsx";
                wb.SaveAs(filePath);
                return filePath; //"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            else return "Отчет не найден в БД";
        }

        public string GenerateUSPDReport(int id) // генерация отчета по УСПД 
        {
            XLWorkbook wb = null;
            IXLWorksheet ws = null;
            try
            {
                wb = new XLWorkbook(_env.WebRootPath + "/Files/Templates/USPDTemplate.xlsx");
                ws = wb.Worksheet(1);
            }
            catch
            {
                return "Ошибка открытия шаблона";
            }
            USPDReport report = _db.USPDReports.Find(id);
            if (report != null)
            {
                double kvvgVolume = 0;
                ws.Cell("G1").Value = report.Substation;
                ws.Cell("M1").Value = "ПО УГЭС " + report.NetRegion.Name;
                ws.Cell("F6").Value = report.Date.ToShortDateString();
                ws.Cell("H6").Value = report.Local;

                string[] names = report.Brigade.Split(";");
                int row = 8;
                foreach (var name in names)
                {
                    ws.Cell("C" + row).Value = name;
                    row++;
                    if (row >= 12) break;
                }

                ws.Cell("I15").Value = "№ сим" + Environment.NewLine + report.PhoneNumber;
                ws.Cell("J15").Value = "'" + report.UspdSerial;
                ws.Cell("L15").Value = "'" + report.PlcSerial;
                if (report.Switches.Count > 1) ws.Cell("N15").Value = "+";
                else ws.Cell("N15").Value = "-";

                ws.Cell("D24").Value = report.Kvvg;
                ws.Cell("D25").Value = report.Gofr;
                ws.Cell("D26").Value = report.Ftp;

                row = 15;
                foreach (var _switch in report.Switches)
                {
                    Device device = _db.Devices.FirstOrDefault(d => d.SerialNumber == _switch.DeviceSerial);
                    if (device != null)
                    {
                        ws.Cell("C" + row).Value = device.DeviceType.Name + Environment.NewLine + "№:" + _switch.DeviceSerial + Environment.NewLine + "ПЛ:" + _switch.DeviceSeal;
                        ws.Cell("E" + row).Value = _switch.TTAk + Environment.NewLine + "№:" + _switch.TTANumber + Environment.NewLine + "Пл:" + _switch.TTASeal;
                        ws.Cell("F" + row).Value = _switch.TTBk + Environment.NewLine + "№:" + _switch.TTBNumber + Environment.NewLine + "Пл:" + _switch.TTBSeal;
                        ws.Cell("G" + row).Value = _switch.TTCk + Environment.NewLine + "№:" + _switch.TTCNumber + Environment.NewLine + "Пл:" + _switch.TTCSeal;
                        ws.Cell("H" + row).Value = "∑=" + _switch.Sum + Environment.NewLine + "T1=" + _switch.T1 + Environment.NewLine + "T2=" + _switch.T2;
                        row += 3;
                        if (_switch.KVVG != 0)
                            kvvgVolume += _switch.KVVG;
                    }
                    else return "ПУ не найден в БД";
                }
                ws.Cell("D27").Value = kvvgVolume;

                string filePath = _env.WebRootPath + "/Files/Отчет_УСПД_" + id + ".xlsx";
                wb.SaveAs(filePath);
                return filePath; //"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            else return "Отчет не найден в БД!";
        }

        public string GenerateWorekerReportByDate(int workerId, DateTime startDate, DateTime endDate) // генерация отчета по работника по двум датам
        {
            WorkerManager workerManager = new WorkerManager(_db);
            List<MounterReportUgesAL> alReports = _db.MounterReportUgesALs.Where(r => r.WorkerId == workerId && (r.ReportState.Description == "принят куратором" || r.ReportState.Description == "импортирован") && r.Date >= startDate && r.Date <= endDate).ToList();
            List<SBReport> sbReports = _db.SBReports.Where(r => r.WorkerId == workerId && (r.ReportState.Description == "принят куратором" || r.ReportState.Description == "импортирован") && r.Date >= startDate && r.Date <= endDate).ToList();
            List<USPDReport> uSPDReports = _db.USPDReports.Where(r => r.WorkerId == workerId && (r.ReportState.Description == "принят куратором" || r.ReportState.Description == "импортирован") && r.Date >= startDate && r.Date <= endDate).ToList();

            XLWorkbook wb = null;
            IXLWorksheet ws = null;
            try
            {
                wb = new XLWorkbook(_env.WebRootPath + "/Files/Templates/WorkerReportByDateTemplate.xlsx");
                ws = wb.Worksheet(1);
            }
            catch
            {
                return "Ошибка открытия шаблона";
            }

            ws.Cell("B2").Value = workerManager.GetWorkerName(workerId);
            ws.Cell("B3").Value = startDate.ToShortDateString() + " - " + endDate.ToShortDateString();
            ws.Cell("H2").Value = DateTime.Now.ToShortDateString();

            int onePDeviceCount = 0;
            int threePDeviceount = 0;
            int threePTTDeviceCount = 0;
            int uspdCount = 0;

            //Подсчет ПУ в отчетах по ВЛ 
            foreach (var report in alReports)
            {
                foreach (var support in report.PowerLineSupports)
                {
                    foreach (var kde in support.KDEs)
                    {
                        foreach (var pu in kde.MounterReportUgesDeviceItems)
                        {
                            Device device = _db.Devices.FirstOrDefault(d => d.SerialNumber == pu.Serial);
                            if (device != null)
                            {
                                if (device.DeviceType.Description.ToLower().Contains("1ф"))
                                    onePDeviceCount++;
                                if (device.DeviceType.Description.ToLower().Contains("3ф"))
                                    threePDeviceount++;
                            }
                        }
                    }
                }
            }

            //Подсчет ПУ в отчетах по ТП
            foreach (var report in sbReports)
            {
                foreach (var pu in report.Switches)
                {
                    Device device = _db.Devices.FirstOrDefault(d => d.SerialNumber == pu.DeviceSerial);
                    if (device != null)
                        threePTTDeviceCount++;
                }
            }

            //Подсчет ПУ в отчетах по УСПД
            foreach (var report in uSPDReports)
            {
                foreach (var pu in report.Switches)
                {
                    Device device = _db.Devices.FirstOrDefault(d => d.SerialNumber == pu.DeviceSerial);
                    if (device != null)
                        threePTTDeviceCount++;
                }
                Device uspd = _db.Devices.FirstOrDefault(d => d.SerialNumber == report.UspdSerial);
                if (uspd != null)
                    uspdCount++;
            }

            ws.Cell("A8").Value = onePDeviceCount;
            ws.Cell("D8").Value = threePDeviceount;
            ws.Cell("G8").Value = threePTTDeviceCount;
            ws.Cell("J8").Value = uspdCount;

            string filePath = _env.WebRootPath + "/Files/Отчет по работнику.xlsx";
            wb.SaveAs(filePath);
            
            return filePath; //"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        }

        public string GenerateUnmountReport(int id) //генерация отчета по демонтажу
        {
            XLWorkbook wb = null;
            IXLWorksheet ws = null;
            try
            {
                wb = new XLWorkbook(_env.WebRootPath + "/Files/Templates/UnmountTemplate.xlsx");
                ws = wb.Worksheet(1);
            }
            catch
            {
                return "Ошибка открытия шаблона";
            }

            UnmountReport report = _db.UnmountReports.Find(id);
            if (report != null)
            {
                WorkerManager workerManager = new WorkerManager(_db);
                ws.Cell("E" + 6).Value = "Акт №" + report.Id;
                ws.Cell("J" + 9).Value = DateTime.Now.ToShortDateString();
                ws.Cell("F" + 20).Value = report.Date;
                NetRegion netRegion = _db.NetRegions.Find(report.NetRegionId);
                if (netRegion != null)
                    ws.Cell("C" + 13).Value = netRegion.FullName;
                int row = 30;
                foreach(var device in report.UnmountedDevices)
                {

                    ws.Cell("C" + row).Value = device.Device.DeviceType.Name;
                    ws.Cell("G" + row).Value ="'" + device.Device.SerialNumber;
                    ws.Cell("G" + row).Style.Font.FontSize = 9;

                    ws.Cell("H" + row).Value = "шт";
                    ws.Cell("j" + row).Value = "1";
                    if (row == 55)
                        row = 65;
                    else row++;
                }

                var curator = _db.Users.Find(report.CuratorId);
                if (curator != null)
                {
                    ws.Cell("G" + 99).Value = "вед. инженер";
                    ws.Cell("K" + 99).Value = curator.Name;
                }
                ws.Cell("G" + 101).Value = "";
                ws.Cell("K" + 101).Value = workerManager.GetShortWorkerName(report.WorkerId);


                ws.Cell("J" + 90).Value = report.UnmountedDevices.Count();
                string filePath = _env.WebRootPath + "/Files/Отчет_демонтаж_" + id + ".xlsx";
                wb.SaveAs(filePath);
                return filePath; //"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            
            else return "Отчет не найден в БД";
        }

        private void ALNextPage(IXLWorksheet ws, int row) //Перенос страницы отчета по ВЛ 
        {
            ws.Range("A1", "S6").CopyTo(ws.Cell("A" + (row + 9)));
            ws.Range("A32", "S36").CopyTo(ws.Cell("A" + (row + 40)));
        }

        private void SBNextPage(IXLWorksheet ws, int row) //Перенос страницы в отчетах по ТП и УСПД 
        {
            ws.Range("A" + (row - 21), "M" + (row - 16)).CopyTo(ws.Cell("A" + (row + 17)));
            ws.Range("A" + (row + 3), "M" + (row + 16)).CopyTo(ws.Cell("A" + (row + 41)));
        }

        private void SBswitchStyle (IXLRange range) //Стиль range для рубильника в отчетах по ТП и УСПД 
        {
            range.Merge();
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }


    }
}
