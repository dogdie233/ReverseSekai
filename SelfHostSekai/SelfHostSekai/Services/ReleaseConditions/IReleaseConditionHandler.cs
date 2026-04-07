using SelfHostSekai.Models;

namespace SelfHostSekai.Services.ReleaseConditions;

/// <summary>
/// ReleaseCondition 解锁事件监听器接口
/// </summary>
public interface IReleaseConditionHandler
{
    /// <summary>
    /// 当 Release Condition 发生解锁时触发
    /// </summary>
    /// <param name="user">当前触发解锁的用户（已被 EF 追踪的主对象）</param>
    /// <param name="newConditionIds">本次刚解锁的 Condition ID</param>
    Task OnConditionsUnlockedAsync(User user, IReadOnlyList<int> newConditionIds);
}