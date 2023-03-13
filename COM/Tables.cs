using System;

namespace COM
{
    public static class RedisKeyPattern
    {
        public const string MAB_PARAMS = "APP:MAB_PARAMS:";
        public const string DCIS_PARAMS = "APP:DCIS_PARAMS:";
        public const string DCP_PARAMS = "APP:DCP_PARAMS:";
        public const string EEC_EAFSPriority = "APP:EEC_EAFSPriority:";
        public const string EEC_PARAMS = "APP:EEC_PARAMS:";
        public const string LSP_DECTCOMB = "APP:LSP_DECTCOMB:";
        public const string LSP_DECTITEMS = "APP:LSP_DECTITEMS:";
        public const string LSP_DECTLIST = "APP:LSP_DECTLIST:";
        public const string LSP_DECTPRIOLS = "APP:LSP_DECTPRIOLS:";
        public const string LSP_PARAMS = "APP:LSP_PARAMS:";
        public const string LSP_PRIORITYITEMS = "APP:LSP_PRIORITYITEMS:";
        public const string LSP_PRIORITYLIST = "APP:LSP_PRIORITYLIST:";
        public const string OCP_CheckPoints = "APP:OCP_CheckPoints:";
        public const string OCP_PARAMS = "APP:OCP_PARAMS:";
        public const string HVLOPCMeasurement = "APP:HVLOPCMeasurement:";
        public const string HVLOPC_Params = "APP:HVLOPC_Params:";

        public const string EEC_TELEGRAM = "APP:EEC_TELEGRAM:";
        public const string EEC_SFSCEAFSPRIORITY = "APP:EEC_SFSCEAFSPRIORITY:";
        public const string SFSC_EAFSPOWER = "APP:SFSC_EAFSPOWER:";
        public const string SFSC_FURNACE_TO_SHED = "APP:SFSC_FURNACE_TO_SHED:";
        public const string SDK_TEMPLATE = "APP:SDK_TEMPLATE:";


    }
    public class MAB_PARAMS_Str
    {
        public string Name;
        public string NetworkPath;
        public string DirectionType;
        public string ScadaType;
        public Guid ID;
    };
    public class OCP_PARAMS_Str
    {
        public string ID;
        public string FUNCTIONNAME;
        public string NAME;
        public string DIRECTIONTYPE;
        public string NETWORKPATH;
        public string SCADATYPE;
    }
    public class OCP_CHECKPOINTS_Str
    {
        public int OCPSHEDPOINT_ID;
        public string NAME;
        public string NETWORKPATH;
        public string DECISIONTABLE;
        public string CHECKOVERLOAD;
        public string DESCRIPTION;
        public string SHEDTYPE;
        public string CATEGORY;
        public float NOMINALVALUE;
        public string LIMITPERCENT;
        public string VOLTAGEENOM;
        public string VOLTAGEDENOM;
        public string POWERNUM;
        public string POWERDENOM;
        public string CHECKPOINT_NETWORKPATH;
        public string Measurement_Id;
        public string IT_Id;
        public string AllowedActivePower_Id;
        public string Average_Id;
        public string Sample_Id;
        public string QualityErr_Id;
    };
    public class HVLOPC_MEAS_Str
    {
        public string ScadaTagName;
        public string KepServerTagName;
        public string Description;
        public int MessageConfiguration;
        public int TagType;
        public string NetworkPath;
        public Guid ID;
        public string Direction;
    };
    public class HVLOPC_PARAM_Str
    {
        public string Name;
        public string IP;
        public string Port;
        public string Description;
    };


    public class LSP_PARAMS_Str
    {
        public string ID;
        public string FUNCTIONNAME;
        public string NAME;
        public string DESCRIPTION;
        public string DIRECTIONTYPE;
        public string NETWORKPATH;
        public string SCADATYPE;
    }
    public class LSP_DECTITEMS_Str
    {
        public string ID;
        public int DECTNO;
        public int DECTITEMNO;
        public string NAME;
        public string NETWORKPATH;
    }

    public class LSP_PRIORITYITEMS_Str
    {
        public string ID_CURR;
        public string ID_CB;
        public string ID_CB_PARTNER;
        public int PRIORITYLISTNO;
        public int ITEMNO;
        public string NETWORKPATH_CURR;
        public string NETWORKPATH_ITEM;
        public string DESCRIPTION;
        public string HASPARTNER;
        public string ADDRESSPARTNER;
    }

    public class EEC_EAFSPRIORITY_Str
    {
        public string ID_CB;
        public string ID_CT;
        public string ID_CB_PARTNER;
        public string CB_NETWORKPATH;
        public string CT_NETWORKPATH;
        public string HASPARTNER;
        public string PARTNERADDRESS;
        public string FURNACE;
    }

    public class LSP_DECTLIST_Str
    {
        public int DECTNO;
        public string NAME;
        public string FULLNAME;
        public int NITEMS;
        public int NCOMBINATIONS;

    }

    public class EEC_PARAMS_Str
    {
        public string ID;
        public string FUNCTIONNAME;
        public string NAME;
        public string DESCRIPTION;
        public string DIRECTIONTYPE;
        public string NETWORKPATH;
        public string SCADATYPE;
        public string TYPE;
    }

    public class EEC_TELEGRAM_Str
    {
        public DateTime TELDATETIME;
        public DateTime SENTTIME;
        public float RESIDUALTIME;
        public float RESIDUALENERGY;
        public float MAXOVERLOAD1;
        public float MAXOVERLOAD2;
        public float RESIDUALENERGYEND;
    }

    public class SFSC_EAFPOWER_Str
    {
        public DateTime TELDATETIME;
        public float SUMATION;
        public float POWERGRP1;
        public float POWERGRP2;
        public float FURNACE1;
        public float FURNACE2;
        public float FURNACE3;
        public float FURNACE4;
        public float FURNACE5;
        public float FURNACE6;
        public float FURNACE7;
        public float FURNACE8;
    }
    public class EEC_SFSCEAFSPRIORITY_Str
    {
        public string CONSUMED_ENERGY_PER_HEAT;
        public string STATUS_OF_FURNACE;
        public string FURNACE;
        public string GROUPNUM;
        public string GROUPNUM_EEC;
        public string REASON;
    }

    public class DCP_PARAMS_Str
    {
        public string ID;
        public string FUNCTIONNAME;
        public string NAME;
        public string DESCRIPTION;
        public string DIRECTIONTYPE;
        public string NETWORKPATH;
        public string SCADATYPE;

    }

    public class SFSC_EAFSPOWER_Str
    {
        public DateTime TELDATETIME;
        public float SUMATION;
        public float POWERGRP1;
        public float POWERGRP2;
        public float FURNACE1;
        public float FURNACE2;
        public float FURNACE3;
        public float FURNACE4;
        public float FURNACE5;
        public float FURNACE6;
        public float FURNACE7;
        public float FURNACE8;
    }
    public class FetchEAFSPriority_Str
    {
        public string CB_NETWORKPATH;
        public string CT_NETWORKPATH;
        public string HASPARTNER;
        public string PARTNERADDRESS;
        public string FURNACE;
        public string CONSUMED_ENERGY_PER_HEAT;
        public string STATUS_OF_FURNACE;
        public string GROUPNUM;
        public string ID_CB;
        public string ID_CT;
        public string ID_CB_PARTNER;
    }
    public class SFSC_FURNACE_TO_SHED_Str
    {
        public DateTime TELDATETIME;
        public string FURNACE;
        public string GROUPPOWER;
        public DateTime SHEADTIME;
        public bool SHEADCOMMAND;

    }

     public class SDK_TEMP_PARAMS_Str
    {
        public string Name;
        public string NetworkPath;
        public string DirectionType;
        public string ScadaType;
        public Guid ID;
    };


}
