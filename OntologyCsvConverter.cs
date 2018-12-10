using System;
using System.Xml.Linq;

namespace AOvechko.Nure.OntoCloud.ConsoleApp
{
    public class OntologyCsvConverter
    {
        public byte[] ConvertXML(XDocument document)
        {
            XDocument doc = XDocument.Load("C:\\Uers\\artio\\Downloads\\root-ontology.owl.xml");

            throw new NotImplementedException();
        }
    }
}
