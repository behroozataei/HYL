using System;
using System.Collections.Generic;

namespace OPC
{
    public class OPCRepository
    {
        public OPCRepository()
        {
            OPCTags = new List<Tag>();
            Connection = new Connection();
        }
        public IList<Tag> OPCTags { get; }
        public Connection Connection { get; }
    }
    public class Tag
    {
        public string ScadaName { get; set; }
        public string NetWorkPath { get; set; } 
        public string OPCName { get; set; }
        public string Description { get; set; }
        public int MessageConfiguration { get; set; }
        public Guid MeasurementId { get; set; }
        public Type Type { get; set; }
        public String Direction { get; set; }    
    }
    public enum Type
    {
        Digital = 0,
        Analog = 1
    }

    
    public class Connection
    {
#nullable enable
        public string? Name { get; set; }
#nullable restore
        public string IP { get; set; }
        public string Port { get; set; }

    }
}
