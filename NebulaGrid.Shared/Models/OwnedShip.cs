namespace NebulaGrid.Shared.Models;

public class OwnedShip
{
    public int PlayerID { get; set; }
    public int ShipID { get; set; }

    public Player? Player { get; set; }
    public Ship? Ship { get; set; }
}
