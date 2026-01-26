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

            // כאן בעתיד תכניס lastRegenUtcTicks מה-save.
            long lastRegenTicksFromSave = 0;

            energyService = new EnergyService(timeProvider, startEnergy, maxEnergy, regenIntervalSeconds, lastRegenTicksFromSave);

            // פעם אחת בתחילת המשחק - כדי לעדכן אנרגיה אם עבר זמן מאז הסשן הקודם
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

        // כשהאפליקציה יוצאת/חוזרת מהשהייה
        private void OnApplicationPause(bool pause)
        {
            if (pause == false)
            {
                energyService.ApplyRegen();
            }
            else
            {
                // פה בעתיד תעשה Save (כשתהיה לך מערכת שמירה)
                // Save should store currentEnergy + lastRegenUtcTicks
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
