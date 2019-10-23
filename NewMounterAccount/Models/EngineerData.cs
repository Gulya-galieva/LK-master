using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NewMounterAccount.Models
{
    public class EngineerData
    {
        public LkStat LkStat { get; set; }
        public List<CuratorCount> CuratorCounts { get; set; }
        public EngineerData()
        {
            CuratorCounts = new List<CuratorCount>();
        }
    }

    public class LkStat
    {
        public int ContractId { get; set; }
        public List<MounterPUCount> MounterPUCounts { get; set; }
        public List<ReportCount> ReportCounts { get; set; }
        public LkStat(int contractId)
        {
            ContractId = contractId;
            MounterPUCounts = new List<MounterPUCount>();
            ReportCounts = new List<ReportCount>();
        }
    }

    public class MounterPUCount
    {
        public int WorkerId { get; set; }
        public string Name { get; set; }
        public int Recived { get; set; }
        public int TotalCount { get; set; }
        public int AcceptedCount { get; set; }
        public int InWorkCount { get; set; }
       
    }

    public class ReportCount
    {
        public string Type { get; set; }
        public int TotalCount { get; set; }
        public int AcceptedCount { get; set; }
        public int InWorkCount { get; set; }
        public int RemarksCount { get; set; }
        public int ImportedCount { get; set; }
        public int SentCount { get; set; }
    }

    public class CuratorCount
    {
        public string CuratorName { get; set; }
        public int CuratorId { get; set; }
        public int Recived { get; set; }
        public int Accepted { get; set; }
    }
}
