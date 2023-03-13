using Opc.Ua;

namespace OPC
{
    public interface IProcessing
    {
        void ScadaPointReceived(NodeId _nodeId, ScadaPoint _scadapoint);
        void AlarmAcked_Processing(ScadaPoint scadaPoint);

    }
}
