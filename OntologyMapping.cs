using System;
using System.Collections.Generic;
using System.Text;

namespace AOvechko.Nure.OntoCloud.ConsoleApp
{
    public class OntologyMapping
    {
        public Dictionary<string, float> OwlClassMapping { get; set; }

        public List<OntologyInstanceMapping> InstanceMappings { get; set; }
    }
}
