using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChekDbManager;

namespace NewMounterAccount.Models
{
    public class DeviceCheckModel
    {
        //Адресс
        public string Local { get; set; }
        public string Street { get; set; }
        public string House { get; set; }
        public string Building { get; set; }
        public string Flat { get; set; }
        public string Comment { get; set; }
        public int SubstationId { get; set; }
  
        //ПУ
        public int Id { get; set; }
        public string OldKDESeal { get; set; }
        public string NewKDESeal { get; set; }
        public string RedSticker { get; set; }
        public string BlueSeal { get; set; }
        public string Serial { get; set; }
        public string DeviceSeal { get; set; }
        public string SideStickerState { get; set; }
        public double Sum { get; set; }
        public double T1 { get; set; }
        public double T2 { get; set; }
        public double P0 { get; set; }
        public double P1 { get; set; }
        public double U1 { get; set; }
        public double U2 { get; set; }
        public double U3 { get; set; }
       
    }
     
    public static class CheckPu 
    {
        public static string Check (CheckDataContext.Device device)
        {
            string state = "БСВ";
            double delta = device.P1 - device.P0;
            if (device.SideStickerState.ToLower() == "поврежден")
            {
                state = "НП";
            }
            else
            {
                //3ф ПУ
                if (device.Serial.StartsWith("009217") || device.Serial.StartsWith("008984") || device.Serial.StartsWith("011347") || device.Serial.StartsWith("009227") || device.Serial.StartsWith("011747") || device.Serial.StartsWith("011888") || device.Serial.StartsWith("011889") || device.Serial.StartsWith("009235"))
                {
                    double p = (Math.Pow(device.U1, 2) / 120 + Math.Pow(device.U2, 2) / 120 + Math.Pow(device.U3, 2) / 120) / 1000; //Расчетная мощность нагрузочника на онове введенных напряжений
                    double pMin = p - p * 0.2;
                    double pMax = p + p * 0.2;
                    //Если дельта не входит в допустимые пределы
                    if (delta < pMin || delta > pMax)
                    {
                        state = "НП";
                    }
                    //Если одно из напряжений < 180 В
                    if (device.U1 < 180 || device.U2 < 180 || device.U3 < 180)
                    {
                        state = "НП";
                    }
                }
                //1ф ПУ
                else
                {
                    double p = (Math.Pow(device.U1, 2) / 120.0)/1000.0;
                    double pMin = p - p * 0.2;
                    double pMax = p + p * 0.2;
                    //Если дельта не входит в допустимые пределы
                    if (delta < pMin || delta > pMax)
                    {
                        state = "НП";
                    }
                    //Если напряжение < 180 В
                    if (device.U1 < 180)
                    {
                        state = "НП";
                    }
                }
            }
            return state;
        }

        public static List<PUCheckModel> GetDevices (int substationId, CheckDataContext _checkDb)
        {
            var devices = from d in _checkDb.Devices
                         where d.SubstationId == substationId
                          select new Models.PUCheckModel
                         {
                             Adress = d.PuAdress,
                             Device = d
                         };
                       

            foreach (var item in devices)
            {
                item.Device.Result = Models.CheckPu.Check(item.Device);
            }
           return devices.ToList();
        }
    }
}
