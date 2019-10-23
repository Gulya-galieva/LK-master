using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbManager;
using ChekDbManager;

namespace NewMounterAccount.Models
{
   
    public class Adress1
    {
        public string Local { get; set; }
        public string Street { get; set; }
        public string House { get; set; }
        public string Building { get; set; }
        public string Flat { get; set; }
        public string Comment { get; set; }
        public int KdeId { get; set; }
    }

    public class Adress2
    {
        public string Local { get; set; }
        public string Comment { get; set; }
        public int KdeId { get; set; }

        public int Id { get; set; }
        public string Street { get; set; }
        public string House { get; set; }
        public string Building { get; set; }
        public string Flat { get; set; }

        public int Id2 { get; set; }
        public string Street2 { get; set; }
        public string House2 { get; set; }
        public string Building2 { get; set; }
        public string Flat2 { get; set; }
    }

    public class Action
    {
        public string Name { get; set; }
        public DateTime DateTime { get; set; }
        public string ActionDone { get; set; }
        public string Comment { get; set; }
    }

    public class KdeCheckStat
    {
        public string WorkerName { get; set; }
        public int WorkerId { get; set; }
        public int Kde1Count { get; set; }
        public int Kde3Count { get; set; }
    }

    public class SubstationStat
    {
        public int Kde1Count { get; set; }
        public int Kde3Count { get; set; }
    }
    
    public class PUCheckModel
    {
        public CheckDataContext.PuAdress Adress { get; set; }
        public CheckDataContext.Device Device { get; set; }
    }
}
