namespace AiResearchers.Core.Orchestration;

public interface IResearchCancellation
{
    // Регистрирует токен выполнения задачи; возвращает связанный CTS для линковки во worker.
    CancellationTokenSource Register(Guid researchTaskId, CancellationToken linkedTo);
    void Complete(Guid researchTaskId);
    bool Cancel(Guid researchTaskId);
}
