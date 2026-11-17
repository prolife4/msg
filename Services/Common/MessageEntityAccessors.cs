using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Vmx.Common.Models.DTO;
using Vmx.Messenger.Models;

namespace Vmx.Messenger.Services.Common;

/// <summary>
/// Позволяет описать поля ORM-сущности сообщения (ClientId/Status/DateCreated/AttemptCount)
/// как expression'ы, компонируемые в SQL-translatable запросы LINQ.
/// Используется базовой моделью MessengerDataModelBase для обобщения по разным сущностям.
/// </summary>
internal sealed class MessageEntityAccessors<TMessageEntity>
    where TMessageEntity : class
{
    public required Expression<Func<TMessageEntity, int?>> ClientId { get; init; }
    public required Expression<Func<TMessageEntity, int?>> Status { get; init; }
    public required Expression<Func<TMessageEntity, DateTime?>> DateCreated { get; init; }
    public required Expression<Func<TMessageEntity, int?>> AttemptCount { get; init; }

    public Expression<Func<TMessageEntity, bool>> BelongsToClient(int clientId) =>
        PropertyEquals(ClientId, clientId);

    // Компилированные предикаты — для фильтрации на клиенте
    // (скомпилированные делегаты внутри Expression не транслируются в SQL).
    public Func<TMessageEntity, bool> ClientIs(int clientId)
    {
        var filter = BelongsToClient(clientId).Compile();
        return m => filter(m);
    }

    public Func<TMessageEntity, bool> OlderThanNotInQueueCompiled(DateTime olderThan)
    {
        var filter = OlderThanNotInQueue(olderThan).Compile();
        return m => filter(m);
    }

    /// <summary>Статусы "к отправке": InQueue либо Failed с остатком попыток (maxAttempts &lt;= 0 — только InQueue).</summary>
    public Expression<Func<TMessageEntity, bool>> PendingFilter(int maxAttempts = 0)
    {
        var inQueue = PropertyEquals(Status, (int)MessageStatus.InQueue);
        if (maxAttempts <= 0)
            return inQueue;

        // Status == Failed && AttemptCount < maxAttempts
        var failed = PropertyEquals(Status, (int)MessageStatus.Failed);
        Expression<Func<TMessageEntity, bool>> attemptsLeft =
            Combine(failed, LessThan(AttemptCount, maxAttempts), Expression.AndAlso);

        return Combine(inQueue, attemptsLeft, Expression.OrElse);
    }

    public Func<TMessageEntity, bool> PendingFilterCompiled(int maxAttempts = 0)
    {
        var filter = PendingFilter(maxAttempts).Compile();
        return m => filter(m);
    }

    private static Expression<Func<TMessageEntity, bool>> PropertyEquals<TValue>(
        Expression<Func<TMessageEntity, TValue>> selector, TValue value) =>
        CombineWithConstant(selector, body => Expression.Equal(body, Convert(value, body.Type)));

    private static Expression<Func<TMessageEntity, bool>> LessThan(
        Expression<Func<TMessageEntity, int?>> selector, int value) =>
        CombineWithConstant(selector, body => Expression.LessThan(body, Convert(value, body.Type)));

    /// <summary>Старые сообщения: созданы до olderThan и уже не в очереди (для удаления истории).</summary>
    public Expression<Func<TMessageEntity, bool>> OlderThanNotInQueue(DateTime olderThan)
    {
        var old = CombineWithConstant(DateCreated, body => Expression.LessThanOrEqual(body, Convert(olderThan, body.Type)));
        var notQueued = Not(PropertyEquals(Status, (int)MessageStatus.InQueue));
        return Combine(old, notQueued, Expression.AndAlso);
    }

    private static Expression<Func<TMessageEntity, bool>> CombineWithConstant<TValue>(
        Expression<Func<TMessageEntity, TValue>> selector, Func<Expression, Expression> predicate)
    {
        var entity = selector.Parameters[0];
        return Expression.Lambda<Func<TMessageEntity, bool>>(predicate(selector.Body), entity);
    }

    private static Expression Convert(object value, Type target) =>
        Expression.Convert(Expression.Constant(value), target);

    private static Expression<Func<TMessageEntity, bool>> Not(Expression<Func<TMessageEntity, bool>> operand)
    {
        var entity = operand.Parameters[0];
        return Expression.Lambda<Func<TMessageEntity, bool>>(Expression.Not(operand.Body), entity);
    }

    private static Expression<Func<TMessageEntity, bool>> Combine(
        Expression<Func<TMessageEntity, bool>> left,
        Expression<Func<TMessageEntity, bool>> right,
        Func<Expression, Expression, Expression> combine)
    {
        var entity = left.Parameters[0];
        var body = combine(left.Body, ReplaceParameter(right.Body, right.Parameters[0], entity));
        return Expression.Lambda<Func<TMessageEntity, bool>>(body, entity);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression oldParam, ParameterExpression newParam) =>
        new ParameterReplacer(oldParam, newParam).Visit(expression);

    private sealed class ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == oldParam ? newParam : base.VisitParameter(node);
    }
}
