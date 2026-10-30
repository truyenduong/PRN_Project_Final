using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class AssignmentNotifier(
    IDbContextFactory<AppDbContext> contextFactory,
    ILogger<AssignmentNotifier> logger) : IAssignmentNotifier
{
    public async Task NotifyTaskAssignmentAsync(long taskId, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var task = await dbContext.Tasks.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken);
            if (task is null)
            {
                logger.LogWarning("Cannot notify task assignment because task {TaskId} was not found", taskId);
                return;
            }

            if (task.AssigneeId is not long assigneeId)
            {
                return;
            }

            dbContext.Notifications.Add(new NotificationEntity
            {
                UserId = assigneeId,
                Type = $"TASK_ASSIGNED:{task.Id}",
                Content = $"You have been assigned a task: \"{task.Title}\"",
                IsRead = false
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to write task assignment notification for task {TaskId}", taskId);
        }
    }

    public async Task NotifySubtaskAssignmentAsync(long subtaskId, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);
            var subtask = await dbContext.Subtasks.AsNoTracking()
                .IgnoreQueryFilters()
                .Include(item => item.Task)
                .SingleOrDefaultAsync(item => item.Id == subtaskId, cancellationToken);
            if (subtask is null)
            {
                logger.LogWarning(
                    "Cannot notify subtask assignment because subtask {SubtaskId} was not found", subtaskId);
                return;
            }

            if (subtask.AssigneeId is not long assigneeId)
            {
                return;
            }

            var content = !string.IsNullOrWhiteSpace(subtask.Task.Title)
                ? $"You have been assigned a subtask: \"{subtask.Title}\" in task: \"{subtask.Task.Title}\""
                : $"You have been assigned a subtask: \"{subtask.Title}\"";
            dbContext.Notifications.Add(new NotificationEntity
            {
                UserId = assigneeId,
                Type = $"SUBTASK_ASSIGNED:{subtask.TaskId}",
                Content = content,
                IsRead = false
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to write subtask assignment notification for subtask {SubtaskId}", subtaskId);
        }
    }
}
