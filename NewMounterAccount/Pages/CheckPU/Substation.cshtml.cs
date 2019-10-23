using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChekDbManager;
using DbManager;
using NewMounterAccount.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NewMounterAccount.Pages.CheckPU
{
    public class SubstationModel : PageModel
    {
        private readonly StoreContext _storeDb;
        private readonly CheckDataContext _checkDb;

        public CheckDataContext.Substation Substation { get; set; }
        public List<Models.Action> Actions { get; set; }
        public List<Models.PUCheckModel> Devices { get; set; }

        public SubstationModel(StoreContext storeDb, CheckDataContext checkDb)
        {
            _storeDb = storeDb; //БД склада
            _checkDb = checkDb; //БД выверки
            Actions = new List<Models.Action>();
            Devices = new List<Models.PUCheckModel>();
        }
        public void OnGet(int id)
        {
            Substation = _checkDb.Substations.Find(id);
            var actions = from p in _checkDb.SubstationActions
                          where p.SubstationId == id
                          select new {
                              Action = p,
                              ActionType = p.SubstationActionType
                          };

           

            foreach (var item in actions)
            {
                User user = _storeDb.Users.Find(item.Action.StoreUserId);
                Actions.Add(new Models.Action
                {
                    Name = user.Name,
                    DateTime = item.Action.Date,
                    ActionDone = item.ActionType.Description,
                    Comment = item.Action.Comment
                });
            } //Последние действия с подстанцией

            Devices = Models.CheckPu.GetDevices(Substation.Id, _checkDb);
        }
    }
}