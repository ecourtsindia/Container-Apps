using Newtonsoft.Json;

namespace eCourts.Shared.Models
{
    public class QueueMessage 
    { 
        public QueueMessageData data { get; set; } = new();
    }
    
    public class QueueMessageData 
    { 
        public string url { get; set; } = string.Empty;
    }

    public class MarkerConversionRequest
    {
        public string PdfBlobUrl { get; set; } = string.Empty;
        public string CnrNumber { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
    }

    public class PdfSigningRequest
    {
        public string PdfBlobPath { get; set; } = string.Empty;
        public string CnrNumber { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
    }

    public class CourtData 
    { 
        public Court court { get; set; } = new();
        public CauseList? civilCauseList { get; set; }
        public CauseList? criminalCauseList { get; set; }
    }
    
    public class Court 
    { 
        public string state_code { get; set; } = string.Empty;
        public string dist_code { get; set; } = string.Empty;
        public string court_code { get; set; } = string.Empty;
        public string court_no { get; set; } = string.Empty;
        public string court_id { get; set; } = string.Empty;
        public string court_name { get; set; } = string.Empty;
        public string complex_name { get; set; } = string.Empty;
        public string nationalstate_code { get; set; } = string.Empty;
    }
    
    public class CauseList 
    { 
        public string listDate { get; set; } = string.Empty;
        public List<Case> cases { get; set; } = new();
    }
    
    public class Case 
    { 
        public string caseNumber { get; set; } = string.Empty;
        public string caseType { get; set; } = string.Empty;
        public string partyName { get; set; } = string.Empty;
        public string advocate { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string nextHearingDate { get; set; } = string.Empty;
        public string caseNoForApi { get; set; } = string.Empty;
        public CaseHistoryData? caseHistoryData { get; set; }
    }
    
    public class CaseHistoryData 
    { 
        public CaseDetails? caseDetails { get; set; }
        public CaseStatus? caseStatus { get; set; }
        public Parties? parties { get; set; }
        public List<object> acts { get; set; } = new();
        public object? firDetails { get; set; }
        public List<object> caseHistory { get; set; } = new();
        public List<InterimOrder> interimOrders { get; set; } = new();
        public List<object> processes { get; set; } = new();
        public List<object> iaStatus { get; set; } = new();
    }
    
    public class CaseDetails 
    { 
        public string cnrNumber { get; set; } = string.Empty;
        public string caseType { get; set; } = string.Empty;
        public string filingNumber { get; set; } = string.Empty;
        public string filingDate { get; set; } = string.Empty;
        public string registrationNumber { get; set; } = string.Empty;
        public string registrationDate { get; set; } = string.Empty;
    }
    
    public class CaseStatus 
    { 
        public string firstHearingDate { get; set; } = string.Empty;
        public string nextHearingDate { get; set; } = string.Empty;
        public string caseStage { get; set; } = string.Empty;
        public string courtNumberAndJudge { get; set; } = string.Empty;
        public string decisionDate { get; set; } = string.Empty;
    }
    
    public class Parties 
    { 
        public string petitionerText { get; set; } = string.Empty;
        public string respondentText { get; set; } = string.Empty;
    }
    
    public class InterimOrder 
    { 
        public string orderNumber { get; set; } = string.Empty;
        public string orderDate { get; set; } = string.Empty;
        public OrderDetails? orderDetails { get; set; }
    }
    
    public class OrderDetails
    {
        public string text { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public string blob_url { get; set; } = string.Empty;
        public string markdown_url { get; set; } = string.Empty;
        public string truecopy_url { get; set; } = string.Empty;
    }
} 