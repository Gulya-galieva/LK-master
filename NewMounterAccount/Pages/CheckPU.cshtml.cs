using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ChekDbManager;
using NewMounterAccount.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using DbManager;

namespace NewMounterAccount.Pages
{
    public class PUModel : PageModel
    {
        private readonly StoreContext _storeDb;
        private readonly CheckDataContext _checkDb;

        public SelectList NetRegions { get; set; }
        public List<CheckDataContext.Substation> Substations { get; set; }

        

        public PUModel(StoreContext storeDb, CheckDataContext checkDb)
        {
            _storeDb = storeDb;
            _checkDb = checkDb;
        }
                    

        public void OnGet()
        {
            GetNetRegions();
        }

        private void GetNetRegions()
        {
            List<DropDownItem> dropDownNetRegions = new List<DropDownItem> { new DropDownItem { Name = "Выберите РЭС", Id = 0 } };
            foreach (var item in _checkDb.NetRegions)
            {
                dropDownNetRegions.Add(new DropDownItem { Name = item.Name, Id = item.Id });
            }
            SelectList netRegions = new SelectList(dropDownNetRegions, "Id", "Name");
            NetRegions = netRegions;
        }
    }
}