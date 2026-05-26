using FactoryGame.Contracts.Boards;

namespace FactoryGame.Web;

internal static class PlanLayout
{
    public static (double X, double Y) GetPosition(MachineDto machine, int index) =>
        PlanMachineLayout.GetPosition(machine, index);

    public static MachineDto WithPosition(MachineDto machine, double x, double y) =>
        PlanMachineLayout.WithPosition(machine, x, y);
}
