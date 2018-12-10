using System;
using System.Collections.Generic;
using System.Text;

namespace AOvechko.Nure.OntoCloud.ConsoleApp
{
    public class OntologyInstanceMapping
    {
        public string RdfResourceName { get; set; }

        public Dictionary<string, float> PropertiesValue { get; set; }

        public string RdfsLabel { get; set; }
    }
}
