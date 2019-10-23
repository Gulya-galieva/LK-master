using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ChekDbManager;
using DbManager;
using NewMounterAccount.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using Newtonsoft.Json;

namespace NewMounterAccount.Controllers
{
    public class KdeCheckController : Controller
    {
        IHostingEnvironment _env;
        CheckDataContext _db;
        StoreContext _storeDb;
        public KdeCheckController(IHostingEnvironment env, CheckDataContext db, StoreContext storeDb)
        {
            _env = env;
            _db = db;
            _storeDb = storeDb;
        }

        public IActionResult Check() //Страница выбора РЭСа и ТП 
        {
            int kde1Total = 0;
            int kde3Total = 0;
            List<KdeCheckStat> stat = new List<KdeCheckStat>();
            foreach (var worker in _storeDb.Users.Where(u => u.Role.Name == "mounter" || u.Role.Name=="engineer"))
            {
                stat.Add(new KdeCheckStat { WorkerName = worker.Name, WorkerId = worker.Id });
            }
            foreach (var item in stat)
            {
                foreach (var kde in _db.Kdes.Where(k => k.StoreUserId == item.WorkerId))
                {
                    if (kde.KdeType.Name == "КДЕ-1")
                    {
                        item.Kde1Count++;
                        kde1Total++;
                    }
                    else
                    {
                        item.Kde3Count++;
                        kde3Total++;
                    }
                }
            }
            stat.Add(new KdeCheckStat { WorkerName = "ИТОГО по результатам проверки", Kde1Count = kde1Total, Kde3Count = kde3Total, WorkerId = 0 });
            ViewBag.Stat = stat;
            List<DropDownItem> dropDownNetRegions = new List<DropDownItem> { new DropDownItem { Name = "Выберите РЭС", Id = 0 } };
            foreach (var item in _db.NetRegions)
            {
                dropDownNetRegions.Add(new DropDownItem { Name = item.Name, Id = item.Id });
            }
            SelectList netRegions = new SelectList(dropDownNetRegions, "Id", "Name");
            ViewBag.NetRegions = netRegions;
            return View();
        }

        public async Task<IActionResult> UploadFile(IFormFile file) //Импорт ТП в бд 
        {
            var filePath = _env.WebRootPath + "/Files/Temp/Subs.xlsx";
            if (file.Length > 0)
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }
            try
            {
                using (ClosedXML.Excel.XLWorkbook wb = new ClosedXML.Excel.XLWorkbook(filePath))
                {
                    bool substationFound = false;
                    int addedCount = 0;

                    var ws = wb.Worksheet(1);
                    int row = 2;

                    var netRegion = ws.Cell("B" + row).Value.ToString();
                    var substation = ws.Cell("C" + row).Value.ToString();

                    while (substation != "") //пока в столбце ТП/РП есть значение
                    {
                        var dNetRegion = _db.NetRegions.FirstOrDefault(r => r.Name == netRegion);

                        foreach (var dSubstation in _db.Substations) //Проверка наличия подстанции в БД
                        {
                            if (dSubstation.Name == substation && dSubstation.NetRegionId == dNetRegion.Id) // Если подстанция уже есть в РЭСе
                            {
                                substationFound = true;
                                break;
                            }
                        }

                        if (!substationFound) //Если подстанции не было в БД
                        {

                            if (dNetRegion != null)
                            {
                                _db.Substations.Add(new CheckDataContext.Substation { Name = substation, NetRegionId = dNetRegion.Id });
                                addedCount++;
                                _db.SaveChanges();
                            }
                            else
                            {
                                ViewData["error"] = "РЭС " + netRegion + " не найден в БД!";
                                return View("Check");
                            }
                        }

                        substationFound = false;
                        row++;
                        netRegion = ws.Cell("B" + row).Value.ToString();
                        substation = ws.Cell("C" + row).Value.ToString();
                    }
                    ViewData["info"] = "Добавлено " + addedCount + " подстанций!";
                }
            }

            catch (Exception ex)
            {
                ViewData["error"] = ex.Message;
            }

               
            List<DropDownItem> dropDownNetRegions = new List<DropDownItem> { new DropDownItem { Name = "Выберите РЭС", Id = 0 } };
            foreach (var item in _db.NetRegions)
            {
                dropDownNetRegions.Add(new DropDownItem { Name = item.Name, Id = item.Id });
            }
            SelectList netRegions = new SelectList(dropDownNetRegions, "Id", "Name");
            ViewBag.NetRegions = netRegions;
            return View("Check");
           
        }

        public  IActionResult GetSubstations(int netRegionId) //Получить список подстанций по РЭСу 
        {
            List<DropDownItem> dropDownSubstations = new List<DropDownItem> ();
            dropDownSubstations.Add(new DropDownItem { Id = 0, Name = "Выберите ТП/РП" });

            foreach (var item in _db.Substations.Where(s => s.NetRegionId == netRegionId))
            {
                dropDownSubstations.Add(new DropDownItem { Name = item.Name, Id = item.Id });
            }

            SelectList netRegions = new SelectList(dropDownSubstations, "Id", "Name");
            return PartialView("_substations", netRegions);
        }

        public async Task<IActionResult> Substation (int id) //Страница подстанции с функционалом добавления КДЕ 
        {
            var substation = await _db.Substations.FindAsync(id);
            List<DropDownItem> dropDownKdeTypes = new List<DropDownItem>();

            foreach (var item in _db.KdeTypes)
            {
                dropDownKdeTypes.Add(new DropDownItem { Name = item.Name, Id = item.Id });
            }
            SelectList kdeTypes = new SelectList(dropDownKdeTypes, "Id", "Name");
            SubstationStat substationStat = new SubstationStat();
            foreach(var kde in substation.Kdes)
            {
                if (kde.KdeType.Name == "КДЕ-1")
                    substationStat.Kde1Count++;
                else
                    substationStat.Kde3Count++;
            }
            ViewBag.SubStat = substationStat;
            ViewBag.KdeTypes = kdeTypes;
            ViewBag.Kdes = _db.Kdes.Where(k => k.SubstationId == id);
            ViewBag.Actions = GetSubstationActions(id);
            


            if (substation != null)
                return View(substation);
            else
            {
                ViewData["error"] = "ТП не найдена в БД!";
                return View("~/Views/Shared/_ErrorPage.cshtml");
            }
        }

        public async Task<IActionResult> GetSubstationStat (int id) //Статистика по подстанции
        {
            var substation = await _db.Substations.FindAsync(id);
            SubstationStat substationStat = new SubstationStat();
            foreach (var kde in substation.Kdes)
            {
                if (kde.KdeType.Name == "КДЕ-1")
                    substationStat.Kde1Count++;
                else
                    substationStat.Kde3Count++;
            }
            return PartialView("_substationStat", substationStat);
        }

        public IActionResult AddKde (int substationId, int kdeTypeId, int count) //Добавить КДЕ к подстанции
        {
            int userId = 0;
            User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);
            userId = user.Id;
            
            CheckDataContext.KdeType type = _db.KdeTypes.Find(kdeTypeId);
            if (count >= 1 && count <= 50)
            {
                for (int i = 0; i < count; i++)
                {
                    _db.Kdes.Add(new CheckDataContext.Kde { SubstationId = substationId, KdeTypeId = kdeTypeId, KdeType = type, StoreUserId = userId });
                }
            }
            else
                _db.Kdes.Add(new CheckDataContext.Kde { SubstationId = substationId, KdeTypeId = kdeTypeId, KdeType = type, StoreUserId = userId });

            _db.SaveChanges();

            CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "добавил кде");
            AddSubstationAction(actionType, substationId, userId, type.Name);
            _db.SaveChanges();
            return PartialView("_kdes", _db.Kdes.Where(k => k.SubstationId == substationId));
        }

        public IActionResult DeleteKde (int kdeId) //Удаление КДЕ 
        {
            int userId = 0;
            User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);
            userId = user.Id;
            ChekDbManager.CheckDataContext.Kde kde = _db.Kdes.Find(kdeId);

            CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "удалил потребителя");
            foreach (var adress in kde.Adresses)
            {
                AddSubstationAction(actionType, kde.SubstationId, userId, adress.Local + ", ул." + adress.Street + ", д." + adress.House + ", корп." + adress.Building + ", кв." + adress.Flat);
                _db.SaveChanges();
            }
            _db.Adresses.RemoveRange(_db.Adresses.Where(a => a.KdeId == kdeId)); //Удаление адресов связанх с удаляемой КДЕ;
            _db.Kdes.Remove(kde);

            actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "удалил кде");
            AddSubstationAction(actionType, kde.SubstationId, userId, kde.KdeType.Name);

            _db.SaveChanges();

            return PartialView("_kdes", _db.Kdes.Where(k => k.SubstationId == kde.SubstationId));
        }

        public IActionResult Add1PU (Adress1 adress) //Добавление одного ареса
        {
            CheckDataContext.Kde kde = _db.Kdes.Find(adress.KdeId);
            if (kde.KdeType.Name != "КДЕ-3-2" && kde.Adresses.Count < 1)
            {
                CheckDataContext.Adress dAdress = new CheckDataContext.Adress
                {
                    Local = adress.Local,
                    Building = adress.Building,
                    House = adress.House,
                    Flat = adress.Flat,
                    Street = adress.Street,
                    KdeId = adress.KdeId,
                    Comment = adress.Comment,
                };
                _db.Adresses.Add(dAdress);
                _db.SaveChanges();

                int userId = 0;
                User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);
                userId = user.Id;
                CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "добавил потребителя");
                AddSubstationAction(actionType, kde.SubstationId, userId, adress.Local+", ул." + adress.Street + ", д." + adress.House+", корп."+adress.Building+", кв."+adress.Flat);
                _db.SaveChanges();
                return PartialView("_kdes", _db.Kdes.Where(k => k.SubstationId == kde.SubstationId));
            }
            else return PartialView("_kdes", _db.Kdes.Where(k => k.SubstationId == kde.SubstationId));

        }

        public IActionResult Add2PU(Adress2 adress) //Добавление двух адресов (КДЕ-3-2) 
        {
            CheckDataContext.Kde kde = _db.Kdes.Find(adress.KdeId);
            if (kde.KdeType.Name == "КДЕ-3-2" && kde.Adresses.Count < 2)
            {
                CheckDataContext.Adress dAdress1 = new CheckDataContext.Adress
                {
                    Local = adress.Local,
                    Building = adress.Building,
                    House = adress.House,
                    Flat = adress.Flat,
                    Street = adress.Street,
                    KdeId = adress.KdeId,
                    Comment = adress.Comment,
                };

                CheckDataContext.Adress dAdress2 = new CheckDataContext.Adress
                {
                    Local = adress.Local,
                    Building = adress.Building2,
                    House = adress.House2,
                    Flat = adress.Flat2,
                    Street = adress.Street2,
                    KdeId = adress.KdeId,
                    Comment = adress.Comment,
                };

                _db.Adresses.Add(dAdress1);
                _db.Adresses.Add(dAdress2);
                _db.SaveChanges();

                int userId = 0;
                User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);
                userId = user.Id;
                CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "добавил потребителя");
                AddSubstationAction(actionType, kde.SubstationId, userId, dAdress1.Local + ", ул." + dAdress1.Street + ", д." + dAdress1.House + ", корп." + dAdress1.Building + ", кв." + dAdress1.Flat);
                AddSubstationAction(actionType, kde.SubstationId, userId, dAdress2.Local + ", ул." + dAdress2.Street + ", д." + dAdress2.House + ", корп." + dAdress2.Building + ", кв." + dAdress2.Flat);
                _db.SaveChanges();
                return PartialView("_kdes", _db.Kdes.Where(k => k.SubstationId == kde.SubstationId));
            }
            else return PartialView("_kdes", _db.Kdes.Where(k => k.SubstationId == kde.SubstationId));
        }

        public string Open1PU (int kdeId) //Открытие формы с двумя потребителями 
        {
            CheckDataContext.Adress tAdress = new CheckDataContext.Adress();
            CheckDataContext.Adress adress = _db.Adresses.LastOrDefault(a => a.KdeId == kdeId);
            tAdress.Local = adress.Local;
            tAdress.Street = adress.Street;
            tAdress.House = adress.House;
            tAdress.Building = adress.Building;
            tAdress.Flat = adress.Flat;
            tAdress.Comment = adress.Comment;
            tAdress.Id = adress.Id;
          
            return JsonConvert.SerializeObject(tAdress);
        }

        public IActionResult Edit1PU(CheckDataContext.Adress adress) //Редактирование КДЕ с одним потребителями
        {
            
            var dAdress = _db.Adresses.Find(adress.Id);
            CheckDataContext.Kde kde = _db.Kdes.Find(dAdress.KdeId);

            int userId = 0;
            User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);
            userId = user.Id;
            CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "изменил потребителя");
            AddSubstationAction(actionType, kde.SubstationId, userId, dAdress.Local + ", ул." + dAdress.Street + ", д." + dAdress.House + ", корп." + dAdress.Building + ", кв." + dAdress.Flat + " на " +
                adress.Local + ", ул." + adress.Street + ", д." + adress.House + ", корп." + adress.Building + ", кв." + adress.Flat);

            dAdress.Local = adress.Local;
            dAdress.Street = adress.Street;
            dAdress.House = adress.House;
            dAdress.Building = adress.Building;
            dAdress.Flat = adress.Flat;
            dAdress.Comment = adress.Comment;

            _db.SaveChanges();

            return PartialView("_kdes", _db.Kdes.Where(k => k.SubstationId == kde.SubstationId));
        }

        public string Open2PU(int kdeId) //Открытие формы с двумя потребителями 
        {
            CheckDataContext.Kde kde = _db.Kdes.Find(kdeId);

            CheckDataContext.Adress adress = kde.Adresses.ToList()[0];
            CheckDataContext.Adress adress2 = kde.Adresses.ToList()[1];

            Adress2 tAdress = new Adress2();
            tAdress.Local = adress.Local;
            tAdress.Comment = adress.Comment;

            tAdress.Street = adress.Street;
            tAdress.House = adress.House;
            tAdress.Building = adress.Building;
            tAdress.Flat = adress.Flat;
            tAdress.Id = adress.Id;

            tAdress.Street2 = adress2.Street;
            tAdress.House2 = adress2.House;
            tAdress.Building2 = adress2.Building;
            tAdress.Flat2 = adress2.Flat;
            tAdress.Id2 = adress2.Id;

            return JsonConvert.SerializeObject(tAdress);
        }

        public IActionResult Edit2PU (Adress2 adress) //Редактирование формы с двумя потребителями 
        {
            CheckDataContext.Adress adress1 = _db.Adresses.Find(adress.Id);
            CheckDataContext.Adress adress2 = _db.Adresses.Find(adress.Id2);
            CheckDataContext.Kde kde = _db.Kdes.Find(adress1.KdeId);

            int userId = 0;
            User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);
            userId = user.Id;
            CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "изменил потребителя");
            AddSubstationAction(actionType, kde.SubstationId, userId, adress1.Local + ", ул." + adress1.Street + ", д." + adress1.House + ", корп." + adress1.Building + ", кв." + adress1.Flat + " на " +
                adress.Local + ", ул." + adress.Street + ", д." + adress.House + ", корп." + adress.Building + ", кв." + adress.Flat);

            AddSubstationAction(actionType, kde.SubstationId, userId, adress2.Local + ", ул." + adress2.Street + ", д." + adress2.House + ", корп." + adress2.Building + ", кв." + adress2.Flat + " на " +
                adress.Local + ", ул." + adress.Street2 + ", д." + adress.House2 + ", корп." + adress.Building2 + ", кв." + adress.Flat2);

            adress1.Local = adress.Local;
            adress1.Street = adress.Street;
            adress1.House = adress.House;
            adress1.Building = adress.Building;
            adress1.Flat = adress.Flat;
            adress1.Comment = adress.Comment;

            adress2.Local = adress.Local;
            adress2.Street = adress.Street2;
            adress2.House = adress.House2;
            adress2.Building = adress.Building2;
            adress2.Flat = adress.Flat2;
            adress2.Comment = adress.Comment;
            
            _db.SaveChanges();

            return PartialView("_kdes", _db.Kdes.Where(k => k.SubstationId == kde.SubstationId));
        }

        public string CheckKde (int kdeId)//Проверка возможности вдобавить КДЕ к ТП 
        {
            CheckDataContext.Kde kde = _db.Kdes.Find(kdeId);
            if (kde != null)
            {
                if (kde.KdeType.Name == "КДЕ-3-2" && kde.Adresses.Count > 1)
                    return "В КДЕ максимальое кол-во потребителей!";
                if (kde.KdeType.Name != "КДЕ-3-2" && kde.Adresses.Count > 0)
                    return "В КДЕ максимальое кол-во потребителей!";
                return "";
            }
            else return "КДЕ не наден в БД!";
        }

        public IActionResult OverallReport ()
        {
            var filepath = _env.WebRootPath + "/Files/kde.xlsx";
            using (ClosedXML.Excel.XLWorkbook wb = new ClosedXML.Excel.XLWorkbook(_env.WebRootPath + "/Files/Templates/KdeTemplate.xlsx"))
            {
                var ws = wb.Worksheet(1);
                int row = 6;
                int kde1Count = 0;
                int kde3Count = 0;
                int kde1TCount = 0;
                int kde3TCount = 0;
                foreach (var sub in _db.Substations)
                {
                    var range = ws.Range("A" + row, "B" + row);

                    ws.Cell("H2").Value =DateTime.Now.ToShortDateString() + " " +  DateTime.Now.ToLongTimeString();

                    range.Merge();
                    ws.Cell("A" + row).Value = sub.NetRegion.Name;

                    range = ws.Range("C" + row, "F" + row);
                    range.Merge();
                    ws.Cell("C" + row).Value = sub.Name;

                    foreach(var kde in _db.Kdes.Where(k => k.SubstationId == sub.Id))
                    {
                        if (kde.KdeType.Name == "КДЕ-1")
                        {
                            kde1Count++;
                            kde1TCount++;
                        }
                        else
                        {
                            kde3Count++;
                            kde3TCount++;
                        }
                            
                    }

                    range = ws.Range("G" + row, "H" + row);
                    range.Merge();
                    ws.Cell("G" + row).Value = kde1Count;

                    range = ws.Range("I" + row, "J" + row);
                    range.Merge();
                    ws.Cell("I" + row).Value = kde3Count;

                    kde1Count = 0;
                    kde3Count = 0;

                    ws.Range("A" + row, "J" + row).Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                    ws.Range("A" + row, "J" + row).Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

                    row++;
                }
                ws.Cell("C2").Value = kde1TCount;
                ws.Cell("C3").Value = kde3TCount;

                wb.SaveAs(filepath);
            }
            return PhysicalFile(filepath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Отчет КДЕ.xlsx");
        }

        private void AddSubstationAction(CheckDataContext.SubstationActionType actionType, int substationId, int userId, string comment = "") //Добавить действие над подстанцией 
        {
            _db.SubstationActions.Add(new CheckDataContext.SubstationAction
            {
                SubstationActionTypeId = actionType.Id,
                StoreUserId = userId,
                Comment = comment,
                SubstationId = substationId,
                Date = DateTime.Now
            });
        }

        private List<Models.Action> GetSubstationActions(int substationId) //Список последних действий над подстанцией 
        {
            List<Models.Action> result = new List<Models.Action>();
            foreach(var act in _db.SubstationActions.Where(a => a.SubstationId == substationId))
            {
                User user = _storeDb.Users.Find(act.StoreUserId);
                result.Add(new Models.Action
                {
                    Name = user.Name,
                    DateTime = act.Date,
                    ActionDone = act.SubstationActionType.Description,
                    Comment = act.Comment
                });
            }
            return result;
        }
    }
}