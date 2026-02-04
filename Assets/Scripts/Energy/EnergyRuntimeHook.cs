using UnityEngine;
using Game.Common.Time;
using Game.Services.Energy;

namespace Game.Runtime.Energy
{
    public class EnergyRuntimeHook : MonoBehaviour
    {
        private EnergyService energyService;

        [Header("Demo Values (later come from save/config)")]
        [SerializeField] private int startEnergy = 5;
        [SerializeField] private int maxEnergy = 10;
        [SerializeField] private int regenIntervalSeconds = 300; // 5 minutes

        private void Awake()
        {
            TimeProvider timeProvider = new TimeProvider();

            long lastRegenTicksFromSave = 0;

            energyService = new EnergyService(timeProvider, startEnergy, maxEnergy, regenIntervalSeconds, lastRegenTicksFromSave);

            //UPDATE ENERTY
            energyService.ApplyRegen();

            Debug.Log("Energy on start: " + energyService.GetCurrent() + "/" + energyService.GetMax());
        }

        // כשהאפליקציה חוזרת לפוקוס (אנדרואיד/אייפון מאוד רלוונטי)
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus == true)
            {
                energyService.ApplyRegen();
            }
        }

        // WHEN STOPPING PUSE
        private void OnApplicationPause(bool pause)
        {
            if (pause == false)
            {
                energyService.ApplyRegen();
            }
            else
            {
                //SAVE SYSTEM
            }
        }

        // לדוגמה: תחבר את זה לכפתור UI "Draw Card"
        public void OnClickDraw()
        {
            int drawCost = 1;

            bool success = energyService.TrySpend(drawCost);

            if (success == true)
            {
                Debug.Log("Drew a card! Energy now: " + energyService.GetCurrent());
            }
            else
            {
                Debug.Log("Not enough energy!");
            }
        }
    }
}
