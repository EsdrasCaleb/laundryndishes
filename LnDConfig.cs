using UnityEngine;

namespace Packages.LaundryNDishes
{
    [CreateAssetMenu(fileName = "LnDConfig", menuName = "Project/Laundry Dishes Configuration\"", order = 1)]
    public class LnDConfig : ScriptableObject
    {
        public string llmServer = "http://localhost:5000"; // Default value
        public string llmApiKey = "";
        public string llmModel = "";
        public double llTemperature = 0.7;
        public int llMaxTokens = 150;
    }
}