using Mars.Interfaces.Environments;

namespace SOHMultimodalModel.Model;

/// <summary>
///     A dock worker agent is a <see cref="Traveler{TLayer}" /> that tries to reach his/her goal with the ferry modality
/// </summary>
public class DockWorker : Traveler<DockWorkerLayer>
{
    public bool OnReturn { get; set; }

    public override void Init(DockWorkerLayer layer)
    {
        base.Init(layer);

        Gender = GenderType.Male;
        EnableCapability(ModalChoice.Ferry);
    }


    protected override MultimodalRoute FindMultimodalRoute()
    {
        return MultimodalLayer.Search(this, Position, GoalPosition, ModalChoice.Ferry);
    } //TODO delete?
}