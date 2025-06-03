namespace eCourts.Shared
{
    public static class Configuration
    {
        // Azure Storage Configuration
        public static readonly string ConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ?? 
            "BlobEndpoint=https://dontdestroystackdcc0tfq6.blob.core.windows.net/;QueueEndpoint=https://dontdestroystackdcc0tfq6.queue.core.windows.net/;FileEndpoint=https://dontdestroystackdcc0tfq6.file.core.windows.net/;TableEndpoint=https://dontdestroystackdcc0tfq6.table.core.windows.net/;SharedAccessSignature=sv=2024-11-04&ss=bfqt&srt=sco&sp=rwdlacupiytfx&se=2028-05-22T13:00:32Z&st=2025-05-22T05:00:32Z&spr=https&sig=e%2BC7rpXlHvoeOO2Z5pAy4%2FoM0l0rYx27XADMav%2B5cTE%3D";
        
        // Queue Names
        public static readonly string QueueName = "district-causelist-json";
        public static readonly string MarkerConversionQueue = "marker-conversion-requests";
        public static readonly string PdfSigningQueue = "pdf-signing-requests";
        public static readonly string ErrorQueueName = "district-causelist-json-unprocessed-log";
        
        // Table Names
        public static readonly string CauseListsTableName = "districtcauselists";
        public static readonly string CnrTableName = "CNRdistrictcourts";
        
        // Container Names
        public static readonly string PdfContainerName = "district-courts-cnr-master";
        public static readonly string JsonContainerName = "district-court-causelists-raw";
        public static readonly string MarkdownContainerName = "markdown-files";
        public static readonly string FileShareName = "ecourtsndiadocs";
        
        // Custom Domains
        public static readonly string CustomDomain = "https://storage.ecourtsindia.com";
        public static readonly string TrueCopyDomain = "https://ecourtsindia.com/truecopy";
        
        // Performance Settings for Container Apps
        public static readonly int MaxDegreeOfParallelism = Environment.GetEnvironmentVariable("MAX_PARALLELISM") != null 
            ? int.Parse(Environment.GetEnvironmentVariable("MAX_PARALLELISM")!) : 4;
        public static readonly int MaxConsecutiveFailures = 10;
        public static readonly int FailurePauseDurationMinutes = 2;
        public static readonly int BatchDelayMilliseconds = 1000;
        public static readonly int MaxPdfDownloadRetries = 5;
        public static readonly int[] PdfRetryDelaysMilliseconds = [2000, 4000, 8000];
        public static readonly int ConversionTimeoutSeconds = 1800; // 30 minutes
        public static readonly int FileDeletionRetries = 3;
        public static readonly int FileDeletionDelayMs = 2000;
        
        // Python Paths for Container Environment
        public static readonly string PythonDefaultPath = "python3";
        public static readonly string MarkerPythonPath = "/opt/venv/bin/python";
        
        // Timeouts
        public static readonly int MarkerTimeoutSeconds = 1800; // 30 minutes
        public static readonly long MemoryThresholdMb = 4000; // 4GB for container
        public static readonly int MaxPageCount = 2000;
        public static readonly int DefaultPageCount = 100;
        public static readonly int MinOutputSizeBytes = 10;
        
        // Certificate Configuration
        public static readonly string CertificatePath = Environment.GetEnvironmentVariable("CERTIFICATE_PATH") ?? "/app/certs/certificate.pfx";
        public static readonly string OwnerPassword = Environment.GetEnvironmentVariable("CERTIFICATE_PASSWORD") ?? "LetsImproveLaw";
        
        // Processing Settings
        public static readonly int MaxConcurrentConversions = Environment.GetEnvironmentVariable("MAX_CONCURRENT_CONVERSIONS") != null 
            ? int.Parse(Environment.GetEnvironmentVariable("MAX_CONCURRENT_CONVERSIONS")!) : 2;
        
        // Conversion method toggles
        public static readonly bool EnableMarkerConversion = Environment.GetEnvironmentVariable("ENABLE_MARKER") != "false";
        public static readonly bool EnableDoclingConversion = false; // Disabled for container optimization
        public static readonly bool EnableTesseractConversion = false; // Disabled for container optimization
        
        // Processing Settings
        public static readonly int QueuePollingIntervalMs = 5000; // 5 seconds
        public static readonly bool EnableParallelConversion = false;
        public static readonly int MaxRetryAttempts = 5;
        public static readonly bool EnableResourceMonitoring = false;
        public static readonly bool EnablePerformanceLogging = false;
        public static readonly bool EnableVerboseLogging = Environment.GetEnvironmentVariable("ENABLE_VERBOSE_LOGGING") == "true";
        public static readonly int StepDelayMs = 1000;
        public static readonly int MaxStepRetries = 3;
        
        // Processing Delays
        public static readonly int ProcessingDelayMs = 500;
        public static readonly int ConversionDelayMs = 1000;
        public static readonly int DownloadDelayMs = 200;
        public static readonly int UploadDelayMs = 200;
        
        // Completion Settings
        public static readonly int MaxProcessingTimeoutMinutes = 45;
        public static readonly bool ForceStepCompletion = true;
    }
} 