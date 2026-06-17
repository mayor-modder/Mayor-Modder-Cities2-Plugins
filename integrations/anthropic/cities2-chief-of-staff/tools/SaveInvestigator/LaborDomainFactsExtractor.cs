using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class LaborDomainFactsExtractor
{
    public static LaborDomainFacts Extract(
        PopulationDomainFacts populationDomainFacts,
        PopulationLaborSemanticsFacts populationLaborSemanticsFacts,
        PopulationLaborCarrierAuditFacts carrierAuditFacts)
    {
        var workers = populationDomainFacts.LaborSummary.WorkerCitizens;
        var students = populationDomainFacts.LaborSummary.StudentCitizens;
        var workerStudents = populationDomainFacts.LaborSummary.WorkerStudents;
        var withoutRole = populationDomainFacts.LaborSummary.CitizensWithoutWorkerOrStudentRole;
        var laborOverlap = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "labor_overlap", StringComparison.Ordinal));
        var travelActivity = populationLaborSemanticsFacts.Groups.FirstOrDefault(
            group => string.Equals(group.GroupKey, "travel_activity_context", StringComparison.Ordinal));
        var jobPressureCarrier = carrierAuditFacts.Dimensions.FirstOrDefault(
            dimension => string.Equals(dimension.Dimension, "job_pressure", StringComparison.Ordinal));
        var educationPipelineCarrier = carrierAuditFacts.Dimensions.FirstOrDefault(
            dimension => string.Equals(dimension.Dimension, "education_pipeline", StringComparison.Ordinal));
        var blockers = new List<string>();

        if (!string.Equals(jobPressureCarrier?.SupportStatus, "confirmed_structural", StringComparison.Ordinal))
        {
            blockers.Add("job suitability and employer-side hiring pressure remain unresolved");
            blockers.Add($"job pressure semantics remain {jobPressureCarrier?.SupportStatus ?? "unresolved"}");
        }

        if (!string.Equals(educationPipelineCarrier?.SupportStatus, "confirmed_structural", StringComparison.Ordinal))
        {
            blockers.Add($"education pipeline semantics remain {educationPipelineCarrier?.SupportStatus ?? "unresolved"}");
        }

        blockers.AddRange(populationLaborSemanticsFacts.RemainingBlockers);

        return new LaborDomainFacts(
            workers,
            students,
            workerStudents,
            withoutRole,
            "partial",
            $"Labor/workforce facts currently cover {workers} workers, {students} students, {workerStudents} worker-students, and {withoutRole} citizens without worker/student roles. Labor overlap is {laborOverlap?.SupportStatus ?? "unresolved"}, travel/activity context is {travelActivity?.SupportStatus ?? "unresolved"}, and employer-side job pressure remains {jobPressureCarrier?.SupportStatus ?? "unresolved"}.",
            new ReadOnlyCollection<string>(
                [
                    $"coverage:workers={workers}",
                    $"coverage:students={students}",
                    $"coverage:worker_students={workerStudents}",
                    $"coverage:citizens_without_worker_or_student_role={withoutRole}",
                    $"semantics:labor_overlap={laborOverlap?.SupportStatus ?? "unresolved"}",
                    $"semantics:travel_activity_context={travelActivity?.SupportStatus ?? "unresolved"}",
                    $"carrier:job_pressure={jobPressureCarrier?.SupportStatus ?? "unresolved"}",
                    $"carrier:education_pipeline={educationPipelineCarrier?.SupportStatus ?? "unresolved"}"
                ]),
            new ReadOnlyCollection<string>(blockers.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList()));
    }

    public static LaborDomainFacts Extract(PopulationDomainFacts populationDomainFacts)
    {
        return Extract(
            populationDomainFacts,
            new PopulationLaborSemanticsFacts(
                new ReadOnlyCollection<PopulationLaborSemanticGroupFact>([]),
                new ReadOnlyCollection<string>([])),
            new PopulationLaborCarrierAuditFacts(
                new ReadOnlyCollection<PopulationLaborCarrierDimensionFact>([])));
    }
}
