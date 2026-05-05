using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmpPerAppE.Models.kpi
{
    public class AddKPIDto
    {
        public int SessionId { get; set; }
        public string KPIName { get; set; }
        public int EmployeeTypeId { get; set; }
        public int RequestedKPIWeight { get; set; }

        // SubKPI list
        public List<SubKPIDto> SubKPIs { get; set; }
    }

    public class SubKPIDto
    {
        public string Name { get; set; }
        public int Weight { get; set; }
    }

    // Isay add karein taake existing KPI mein naye Sub-KPIs add ho saken
    public class DynamicSubKpiDto
    {
        public int SessionId { get; set; }
        public int KpiId { get; set; }
        public string Name { get; set; }
        public int NewWeight { get; set; }
    }

    public class EditNameDto
    {
        public string Name { get; set; }
    }

    public class EditWeightDto
    {
        public int Weight { get; set; }
    }
}