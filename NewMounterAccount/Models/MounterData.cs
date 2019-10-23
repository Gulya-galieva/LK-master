using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbManager;

namespace NewMounterAccount.Models
{
    public class MounterData
    {
        public string ContractName { get; set; }
        public int ContractId { get; set; }
        public int Recived { get; set; }
        public int RecivedUspd { get; set; }
        public int Accepted { get; set; }
    }

    public class ReportModel
    {
        public Contract Contract { get; set; }
        public NetRegion NetRegion { get; set; }
    }

    public class ReportItem
    {
        public string Substation { get; set; }
        public int Mounted { get; set; }
        public int Accepted { get; set; }
        public DateTime Date { get; set; }
        public int Id { get; set; }
        public string NetRegion { get; set; }
        public string Type { get; set; }
        public string State { get; set; }
    }

    public class MounterReportModel
    {
        public int WorkerId { get; set; }
        public bool DeliveryAvailible { get; set; }
        public List<WorkerDevice> WorkerDevices { get; set; }
        public List<WorkerMaterial> WorkerMaterials { get; set; }

        public MounterReportModel(int workerId, bool deliveryAvailible)
        {
            WorkerDevices = new List<WorkerDevice>();
            WorkerMaterials = new List<WorkerMaterial>();
            WorkerId = workerId;
            DeliveryAvailible = deliveryAvailible;
        }
    }

    public class WorkerDevice
    {
        public int WorkerId { get; set; }
        public string Serial { get; set; }
        public string DeviceType { get; set; }
        public DateTime Date { get; set; }
        public string RowColorCode { get; set; }
        public int ContractId { get; set; }
    }

    public class WorkerMaterial
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public double Volume { get; set; }
    }

    public class ALReportPUModel
    {
        public List<MounterReportUgesDeviceItem> Devices { get; set; }
        public List<AdditionalMaterial> AdditionalMaterials { get; set; }

        public async Task<ALReportPUModel> CreateAsync (int kdeId)
        {
            ALReportPUModel model = new ALReportPUModel();
            model.AdditionalMaterials = new List<AdditionalMaterial>();
            Devices = new List<MounterReportUgesDeviceItem>();
            using (StoreContext db = new StoreContext())
            {
                KDE kde = await db.KDEs.FindAsync(kdeId);
                foreach (var device in kde.MounterReportUgesDeviceItems)
                {
                    model.Devices.Add(device);
                    model.AdditionalMaterials.AddRange(db.AdditionalMaterials.Where(m => m.DeviceId == device.DeviceId));
                }
            }
            return model;
        }
    }

    public class AdditionalMaterialModel
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public double Volume { get; set; }
    }

    public class UnmountedDeviceModel
    {
        public int Id { get; set; }
        public string Serial { get; set; }
        public string Substation { get; set; }
        public string NetRegion { get; set; }
        public string Reason { get; set; }
    }
}
