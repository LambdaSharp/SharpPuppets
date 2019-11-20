using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Sharp
{
    public class Models
    {
        public class PuppeteerSteps {
        
            [JsonProperty("action")]
            public string Action;
        
            [JsonProperty("value")]
            public string Value;
            
            [JsonProperty("selector")]
            public string Selector;
        }

        public class Site {

            [JsonProperty("siteName")]
            public string SiteName;
        
            [JsonProperty("steps")]
            public List<PuppeteerSteps> Steps;
        }

        public class Payload
        {
            [JsonProperty("sites")]
            public List<Site> Sites;
        }
    }
}