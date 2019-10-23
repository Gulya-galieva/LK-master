using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NewMounterAccount.Models
{
    public class CuratorData
    {
        public string MounterName { get; set; }
        public int MounterId { get; set; }
        public int Recived { get; set; }
        public int Accepted { get; set; }
    }
}
