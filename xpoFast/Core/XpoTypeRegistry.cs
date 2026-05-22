namespace XpoFast.Core;

/// <summary>
/// O(1) lookup for XPO element types. Replaces the original O(n) Where-Object scan over 98 items.
/// Key format: "TAG" or "TAG|SubTag" for types with sub-classification (PRN, FTM).
/// </summary>
public static class XpoTypeRegistry
{
    private static readonly Dictionary<string, XpoType> ByTagSubTag;
    private static readonly Dictionary<int, XpoType> ByNodeType;

    public static readonly XpoType[] AllTypes;

    static XpoTypeRegistry()
    {
        AllTypes =
        [
            // Core AOT types
            new(329, 45,  "CLS", "",        "Class",                     ["Classes"],                                          "Classes"),
            new(209, 40,  "DBE", "",        "Enum",                      ["Data Dictionary","Base Enums"],                      "Base Enums"),
            new(228, 41,  "UTE", "",        "ExtendedType",              ["Data Dictionary","Extended Data Types"],             "Extended Data Types"),
            new(204, 44,  "DBT", "",        "Table",                     ["Data Dictionary","Tables"],                         "Tables"),
            new(243, 44,  "VIE", "",        "ViewQuery",                 ["Data Dictionary","Views"],                          "Views"),
            new(236, 44,  "MAP", "",        "TableMap",                  ["Data Dictionary","Maps"],                           "Maps"),
            new(201, 11,  "FRM", "",        "Form",                      ["Forms"],                                            "Forms"),
            new(215,  5,  "JOB", "",        "Job",                       ["Jobs"],                                             "Jobs"),
            new(296,  1,  "FTM", "1",       "DisplayTool",               ["Menu Items","Display"],                             "Display Menu Items"),
            new(296,  2,  "FTM", "2",       "OutputTool",                ["Menu Items","Output"],                              "Output Menu Items"),
            new(296,  3,  "FTM", "3",       "ActionTool",                ["Menu Items","Action"],                              "Action Menu Items"),
            new(218,  4,  "MCR", "",        "Macro",                     ["Macros"],                                           "Macros"),
            new(205, 16,  "MNU", "",        "Menu",                      ["Menus"],                                            "Menus"),
            new(211, 48,  "TCL", "",        "TableCollection",           ["Data Dictionary","Table Collections"],              "Table Collections"),
            new(330, 20,  "QUE", "",        "Query",                     ["Queries"],                                          "Queries"),
            new(  0, 37,  "PRN", "SHARED",  "SharedProject",             ["Projects","Shared"],                                "Shared Projects"),
            new(  0, 38,  "PRN", "PRIVATE", "Private",                   ["Projects","Private"],                               "Private Projects"),
            new(822, 53,  "REF", "",        "Reference",                 ["References"],                                       "References"),
            new(202, 18,  "RG",  "",        "Report",                    ["Reports"],                                          "Reports"),
            new(1426,79,  "RLB", "",        "ReportLibrary",             ["Report Libraries"],                                 "Report Libraries"),
            new( 86, 27,  "RST", "",        "ReportSectionTemplate",     ["Reports","Section Templates"],                      "Report Section Templates"),
            new( 90, 19,  "RGT", "",        "ReportTemplate",            ["Reports","Report Templates"],                       "Report Templates"),
            new(820, 21,  "RES", "",        "Resource",                  ["Resources"],                                        "Resources"),
            new(312, 39,  "CON", "",        "ConfigurationKey",          ["Data Dictionary","Configuration Keys"],             "Configuration Keys"),
            new(313, 36,  "SEC", "",        "SecurityKey",               ["Data Dictionary","Security Keys"],                  "Security Keys"),
            new(207, 72,  "DST", "",        "DataSet",                   ["Data Sets"],                                        "Data Sets"),
            new(311, 15,  "LIC", "",        "LicenseCode",               ["Data Dictionary","License Codes"],                  "License Codes"),
            new(1311,66,  "PRS", "",        "Perspective",               ["Data Dictionary","Perspectives"],                   "Perspectives"),
            new(1321,76,  "SVC", "",        "Service",                   ["Services"],                                         "Services"),
            new(1325,137, "SVG", "",        "ServiceGroup",              ["Service Groups"],                                   "Service Groups"),
            // Web
            new(864, 55,  "WMU", "",        "WebUrlItem",                ["Web","Menu Items","URL"],                           "Web URL Items"),
            new(866, 56,  "WMA", "",        "WebActionItem",             ["Web","Menu Items","Actions"],                       "Web Action Menu Items"),
            new(806, 30,  "WME", "",        "WebMenu",                   ["Web","Menus"],                                      "Web Menus"),
            new(800, 34,  "WFM", "",        "WebForm",                   ["Web","Forms"],                                      "Web Forms"),
            new(873, 59,  "WIT", "",        "WebletItem",                ["Web","Weblets"],                                    "Weblets"),
            new(885, 67,  "WML", "",        "WebModule",                 ["Web","Modules"],                                    "Web Modules"),
            new(203, 52,  "WRG", "",        "WebReport",                 ["Web","Reports"],                                    "Web Reports"),
            new(892, 78,  "WLD", "",        "WebListDef",                ["Web","Files","List Definitions"],                   "Web List Definitions"),
            new(877, 61,  "WSD", "",        "WebSiteDef",                ["Web","Files","List Definitions"],                   "Web Site Definitions"),
            new(887, 73,  "WCL", "",        "WebControl",                ["Web","Files","Web Controls"],                       "Web Controls"),
            new(881, 63,  "WPD", "",        "WebPageDef",                ["Web","Files","Page Definitions"],                   "Web Page Definitions"),
            new(879, 62,  "WPD", "",        "WebSiteTemp",               ["Web","Files","Site templates"],                     "Web Site Templates"),
            new(869, 57,  "WCD", "",        "WebDisplayContentItem",     ["Web","Web Content","Dispaly"],                      "Web Display Content Items"),
            new(871, 58,  "WCO", "",        "WebOutputContentItem",      ["Web","Web Content","Output"],                       "Web Output Content Items"),
            new(890, 75,  "WCM", "",        "WebManagedContentItem",     ["Web","Web Content","Managed"],                      "Web Managed Content Items"),
            new(883, 64,  "WRF", "",        "WebStaticFile",             ["Web","Web Content","Static Files"],                 "Web Static Files"),
            new(875, 60,  "WWP", "",        "WebPart",                   ["Web","Web Content","Web Parts"],                    "Web Parts"),
            // Workflow
            new(1412,68,  "WFL", "",        "WorkflowTemplate",          ["Workflow","Templates"],                             "Workflow Templates"),
            new(1417,69,  "WFT", "",        "WorkflowTask",              ["Workflow","Tasks"],                                 "Workflow Tasks"),
            new(1421,70,  "WFA", "",        "WorkflowApproval",          ["Workflow","Approvals"],                             "Workflow Approvals"),
            new(1423,71,  "WFC", "",        "WorkflowCategory",          ["Workflow","Categories"],                            "Workflow Categories"),
            new(1409,95,  "WFN", "",        "WorkflowAutomatedTask",     ["Workflow","Automated Tasks"],                       "Workflow Automated Tasks"),
            new(1397,139, "WFH", "",        "WorkflowHierarchyProvider", ["Workflow","Providers","Hierarchy Assignment"],      "Workflow Hierarchy Assignment Providers"),
            new(1399,140, "WFP", "",        "WorkflowParticipantProvider",["Workflow","Providers","Participant Assignment"],   "Workflow Participant Assignment Providers"),
            new(1401,141, "WFQ", "",        "WorkflowQueueProvider",     ["Workflow","Providers","Analysis Queue Assignment"], "Workflow Queue Providers"),
            new(1403,142, "WFD", "",        "WorkflowDueDateProvider",   ["Workflow","Providers","Due Date Calculation"],      "Workflow Due Date Providers"),
            // Help
            new(1301,65,  "HPF", "",        "HelpFile",                  ["Help Files"],                                       "Help Files"),
            // AX 2012 Parts
            new(1429,81,  "IPA", "",        "InfoPart",                  ["Parts","Info Parts"],                               "Info Parts"),
            new(1431,82,  "FPA", "",        "FormPart",                  ["Parts","Form Parts"],                               "Form Parts"),
            new(1543,98,  "CUN", "",        "Cue",                       ["Parts","Cues"],                                     "Cues"),
            new(1544,99,  "CGN", "",        "CueGroup",                  ["Parts","Cue Groups"],                               "Cue Groups"),
            // SSRS Reports
            new(1449,93,  "SDS", "",        "SSRSReportDataSource",      ["SSRS Reports","Reports Datasources"],               "SSRS Report Datasources"),
            new(1448,92,  "SXT", "",        "SSRSReportXYChartStyleTemplate",["SSRS Reports","Report Style Templates","XY Chart Style Templates"], "SSRS Report XY Chart Style Templates"),
            new(1447,91,  "STT", "",        "SSRSReportTableStyleTemplate",  ["SSRS Reports","Report Style Templates","Table Style Templates"],    "SSRS Report Table Style Templates"),
            new(1446,90,  "SPT", "",        "SSRSReportPieChartStyleTemplate",["SSRS Reports","Report Style Templates","Pie Chart Style Templates"],"SSRS Report Pie Chart Style Templates"),
            new(1445,89,  "SMT", "",        "SSRSReportMatrixStyleTemplate", ["SSRS Reports","Report Style Templates","Matrix Style Templates"],   "SSRS Report Matrix Style Templates"),
            new(1444,88,  "SLT", "",        "SSRSReportListStyleTemplate",   ["SSRS Reports","Report Style Templates","List Style Templates"],     "SSRS Report List Style Templates"),
            new(1443,87,  "SRL", "",        "SSRSReportLayoutTemplate",      ["SSRS Reports","Report Style Templates","Layout Templates"],         "SSRS Report Layout Templates"),
            new(1439,85,  "SRP", "",        "SSRSReport",                    ["SSRS Reports","Reports"],                                           "SSRS Reports"),
            // Visual Studio Projects
            new(1531,127, "VPY", "",        "VSProject_AXModel",         ["Visual Studio Projects","Dynamics AX Model Projects"], "VSProject Dynamics AX Model Projects"),
            new(1531,128, "VPC", "",        "VSProject_CSharp",          ["Visual Studio Projects","C Sharp Projects"],            "VSProject C Sharp Projects"),
            new(1531,131, "VPY", "",        "VSProject_Analysis",        ["Visual Studio Projects","Analysis Services Projects"],  "VSProject Analysis Projects"),
            // Security
            new(1608,115, "SCP", "",        "SecCodePermission",         ["Security","Code Permissions"],                      "Security Code Permissions"),
            new(1628,134, "SPV", "",        "SecPrivilege",              ["Security","Privileges"],                            "Security Privileges"),
            new(1630,135, "SDT", "",        "SecDuty",                   ["Security","Duties"],                                "Security Duties"),
            new(1626,133, "SRO", "",        "SecRole",                   ["Security","Roles"],                                 "Security Roles"),
            new(1636,136, "SPC", "",        "SecProcessCycle",           ["Security","Process Cycles"],                        "Security Process Cycles"),
            new(1619,119, "SPO", "",        "SecPolicy",                 ["Security","Policies"],                              "Security Policies"),
            // Labels / Docs
            new(831, 117, "LBL", "",        "LabelFile",                 ["Label Files"],                                      "Label Files"),
            new(1527,101, "DCS", "",        "DocSet",                    ["Help Document Sets"],                               "Help Document Sets"),
            // AX 3.0
            new(237, 39,  "FCC", "",        "FeatureKey",                ["Data Dictionary","Feature keys"],                   "Feature keys"),
        ];

        ByTagSubTag = new Dictionary<string, XpoType>(AllTypes.Length, StringComparer.OrdinalIgnoreCase);
        ByNodeType  = new Dictionary<int, XpoType>(AllTypes.Length);

        foreach (var t in AllTypes)
        {
            var key = string.IsNullOrEmpty(t.SubTag) ? t.Tag : $"{t.Tag}|{t.SubTag}";
            ByTagSubTag.TryAdd(key, t);
            if (t.NodeType > 0) ByNodeType.TryAdd(t.NodeType, t);
        }
    }

    public static XpoType? FindByTag(string tag, string subTag = "")
    {
        var key = string.IsNullOrEmpty(subTag) ? tag : $"{tag}|{subTag}";
        return ByTagSubTag.TryGetValue(key, out var t) ? t : null;
    }

    public static XpoType? FindByNodeType(int nodeType) =>
        ByNodeType.TryGetValue(nodeType, out var t) ? t : null;

    public static IEnumerable<XpoType> FindAllByTag(string tag) =>
        AllTypes.Where(t => string.Equals(t.Tag, tag, StringComparison.OrdinalIgnoreCase));
}
