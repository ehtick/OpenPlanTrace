namespace OpenPlanTrace;

public sealed record OpeningRoomConnection(
    string RoomId,
    string? RoomLabel,
    RoomUseKind RoomUseKind,
    IReadOnlyList<string> RoomAdjacencyIds,
    OpeningRoomSide Side,
    PlanPoint? RoomSidePoint,
    PlanPoint? NearestBoundaryPoint,
    double SignedDistanceFromOpening,
    double DistanceToOpening,
    bool SharesHostWall,
    Confidence Confidence,
    IReadOnlyList<string> Evidence);
