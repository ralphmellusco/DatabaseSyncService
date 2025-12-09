using System;
using System.Collections.Generic;

namespace DatabaseSyncService.Models
{
    public class SchemaValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Warnings { get; set; }
        
        public SchemaValidationResult()
        {
            Warnings = new List<string>();
        }
    }
}