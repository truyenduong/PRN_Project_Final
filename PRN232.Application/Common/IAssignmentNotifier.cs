namespace PRN232.Application.Common;

public interface IAssignmentNotifier
{
    Task NotifyTaskAssignmentAsync(long taskId, CancellationToken cancellationToken);
    Task NotifySubtaskAssignmentAsync(long subtaskId, CancellationToken cancellationToken);
}
