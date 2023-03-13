using System;

namespace OPC
{
    public enum SinglePointStatus
    {
        Disappear = 0,
        Appear = 1
    }

    public sealed class ScadaPoint
    {
        public ScadaPoint(Guid id, string name, string networkPath, string direction,Type type)
        {
            Id = id;
            Name = name;
            NetworkPath = networkPath;
            Direction = direction;
            Type = type;

        }

        public string Name { get; }
        public string NetworkPath { get; }
        public Guid Id { get; }
        public int Quality { get; set; }
        public float Value { get; set; }
        public String Direction { get; set; }
        public Type Type { get; set; }


    }

    public enum DigitalStatus
    {
        Intransit = 0,
        Open = 1,
        Close = 2,
        Disturb = 3
    }
}
