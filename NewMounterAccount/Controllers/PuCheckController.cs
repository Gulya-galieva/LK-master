using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChekDbManager;
using DbManager;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NewMounterAccount.Models;
using Newtonsoft.Json;

namespace NewMounterAccount.Controllers
{
    public class PuCheckController : Controller
    {
        IHostingEnvironment _env;
        CheckDataContext _db;
        StoreContext _storeDb;
        public PuCheckController (IHostingEnvironment env, CheckDataContext db, StoreContext storeDb)
        {
            _env = env;
            _db = db;
            _storeDb = storeDb;
        }

        public IActionResult AddDevice (DeviceCheckModel device)
        {
            
            User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);
            CheckDataContext.PuAdress adress = new CheckDataContext.PuAdress
            {
                Local = device.Local,
                Street = device.Street,
                House = device.House,
                Building = device.Building,
                Flat = device.Flat,
                Comment = device.Comment,
                
            };
            _db.PuAdresses.Add(adress);
            _db.SaveChanges();

            CheckDataContext.Device pu = new CheckDataContext.Device
            {
                PuAdressId = adress.Id,
                NewKDESeal = device.NewKDESeal,
                OldKDESeal = device.OldKDESeal,
                BlueSeal = device.BlueSeal,
                RedSticker = device.RedSticker,
                DviceSeal = device.DeviceSeal,
                WorkerId = user.Id,
                P0 = device.P0,
                P1 = device.P1,
                Serial = device.Serial,
                SideStickerState = device.SideStickerState,
                SubstationId = device.SubstationId,
                Sum = device.Sum,
                T1 = device.T1,
                T2 = device.T2,
                U1 = device.U1,
                U2 = device.U2,
                U3 = device.U3,
            };
            _db.Devices.Add(pu);
            _db.SaveChanges();

            CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "добавил пу");
            AddSubstationAction(actionType, device.SubstationId, user.Id, device.Serial);
            _db.SaveChanges();

            return PartialView("/Views/PuCheck/_PU.cshtml", Models.CheckPu.GetDevices(device.SubstationId, _db));
        }

        public IActionResult DeleteDevice(int deviceId, int substationId)
        {

            User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);
            CheckDataContext.Device device = _db.Devices.Find(deviceId);
            _db.Devices.Remove(device);
            _db.PuAdresses.Remove(device.PuAdress);
            _db.SaveChanges();

            CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "удалил пу");
            AddSubstationAction(actionType, device.SubstationId, user.Id, device.Serial);
            _db.SaveChanges();
            return PartialView("/Views/PuCheck/_PU.cshtml", Models.CheckPu.GetDevices(substationId, _db));
        }

        public IActionResult EditDevice (DeviceCheckModel device)
        {
            CheckDataContext.Device oldDevice = _db.Devices.Find(device.Id);
            CheckDataContext.PuAdress oldAdress = oldDevice.PuAdress;
            User user = _storeDb.Users.FirstOrDefault(u => u.Login == User.Identity.Name);

            oldAdress.Local = device.Local;
            oldAdress.Street = device.Street;
            oldAdress.House = device.House;
            oldAdress.Building = device.Building;
            oldAdress.Flat = device.Flat;
            oldAdress.Comment = device.Comment;

            oldDevice.BlueSeal = device.BlueSeal;
            oldDevice.DviceSeal = device.DeviceSeal;
            oldDevice.NewKDESeal = device.NewKDESeal;
            oldDevice.OldKDESeal = device.OldKDESeal;
            oldDevice.P0 = device.P0;
            oldDevice.P1 = device.P1;
            oldDevice.RedSticker = device.RedSticker;
            oldDevice.Serial = device.Serial;
            oldDevice.SideStickerState = device.SideStickerState;
            oldDevice.Sum = device.Sum;
            oldDevice.T1 = device.T1;
            oldDevice.T2 = device.T2;
            oldDevice.U1 = device.U1;
            oldDevice.U2 = device.U2;
            oldDevice.U3 = device.U3;

            _db.SaveChanges();

            CheckDataContext.SubstationActionType actionType = _db.SubstationActionTypes.FirstOrDefault(a => a.Description == "изменил пу");
            AddSubstationAction(actionType, device.SubstationId, user.Id, device.Serial);
            _db.SaveChanges();

            return PartialView("/Views/PuCheck/_PU.cshtml", Models.CheckPu.GetDevices(oldDevice.SubstationId, _db));
        }

        public string OpenDeviceForEdit (int deviceId)
        {
            CheckDataContext.Device device = _db.Devices.Find(deviceId);
            CheckDataContext.PuAdress adress = device.PuAdress;

            device.PuAdress = null;
            device.Substation = null;
            device.SubstationId = 0;
            

            PUCheckModel model = new PUCheckModel { Adress = adress, Device = device };
            return JsonConvert.SerializeObject(model);
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
    }
}