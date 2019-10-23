using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DbManager;
using Microsoft.EntityFrameworkCore;

namespace NewMounterAccount.AppCode
{
    public class DeviceCheck
    {
        StoreContext db;
        public DeviceCheck(StoreContext context)
        {
            db = context;
        }
        public async Task<string> CheckDevice(string SerialNumber, int ContractId, Worker worker)
        {
            Device device = await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == SerialNumber);
            if (device != null)
            {
                if (device.ContractId != ContractId)
                {
                    return "Оборудование [" + SerialNumber + "] не относится к контракту отчета!";
                }
                if (device.CurrentState != "выдача со склада")
                    return "Оборудование [" + SerialNumber + "] невозможно привязать к отчету!";
                else
                {
                    List<DeliveryAct> deliveryActs = new List<DeliveryAct>();
                    foreach (var delivery in device.DeviceDeliveries)
                    {
                        if (delivery.DeliveryAct.DeliveryType.Description == "выдача со склада")
                        {
                            deliveryActs.Add(delivery.DeliveryAct);
                        }
                    }
                    if (deliveryActs.Count == 1)
                    {
                        if (deliveryActs[0].WorkerId != worker.Id)
                            return "Оборудование [" + SerialNumber + "] выдавалось другому работнику!";
                        else return "";
                    }
                    else
                    {
                        var sortedDeliveryActs = from a in deliveryActs orderby a.Date select a;
                        if (sortedDeliveryActs.Last().WorkerId != worker.Id)
                            return "Оборудование [" + SerialNumber + "] выдавалось другому работнику!";
                        else return "";
                    }
                }

            }
            else return "Оборудование [" + SerialNumber + "] не найдено в БД!";
        }

        public string CheckDevice(Device device, Worker worker, KDE kde)
        {
            
            if (device != null)
            {
                if (kde.KDEType.Name != "КДЕ-3-2" && kde.MounterReportUgesDeviceItems.Count > 0)
                    return "В КДЕ максимально допустимое кол-во ПУ!";
                if (kde.KDEType.Name == "КДЕ-3-2" && kde.MounterReportUgesDeviceItems.Count >= 2)
                    return "В КДЕ максимально допустимое кол-во ПУ!";


                if (device.CurrentState != "выдача со склада")
                    return "Оборудование [" + device.SerialNumber + "] невозможно привязать к отчету!";
                else
                {
                    List<DeliveryAct> deliveryActs = new List<DeliveryAct>();
                    foreach (var delivery in device.DeviceDeliveries)
                    {
                        if (delivery.DeliveryAct.DeliveryType.Description == "выдача со склада")
                        {
                            deliveryActs.Add(delivery.DeliveryAct);
                        }
                    }
                    if (deliveryActs.Count == 1)
                    {
                        if (deliveryActs[0].WorkerId != worker.Id)
                            return "Оборудование [" + device.SerialNumber + "] выдавалось другому работнику!";
                        else return "";
                    }
                    else
                    {
                        var sortedDeliveryActs = from a in deliveryActs orderby a.Date select a;
                        if (sortedDeliveryActs.Last().WorkerId != worker.Id)
                            return "Оборудование [" + device.SerialNumber + "] выдавалось другому работнику!";
                        else return "";
                    }
                }
            }
            else return "Оборудование [" + device.SerialNumber + "] не найдено в БД!";
        }
    }
}
