using System;
using System.Collections.Generic;

namespace OPC
{
    public interface IRepository
    {
        ScadaPoint GetScadaPoint(Guid measurementId);
        ScadaPoint GetScadaPoint(String name);
        Guid GetMeasurementID(string opcTagName);
        string GetOPCOutputTageName(Guid MeasurementId);
        IList<Tag> GetTags();
        OPCRepository GetOPCConnectionParams();

    }
}
