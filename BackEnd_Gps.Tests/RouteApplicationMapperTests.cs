using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;

namespace BackEnd_Gps.Tests;

public class RouteApplicationMapperTests
{
    [Fact]
    public void ToDtoListOmitsImagePayloadButKeepsEvidenceMetadataAndNotes()
    {
        var image = RoutePointEvidence.Create("image", "data:image/png;base64,AQ==", "Foto de entrega");
        var note = RoutePointEvidence.Create("note", "Entregado", null);
        var origin = RoutePoint.Create(-0.18, -78.46, "Origen");
        var stop = RoutePoint.Create(
            -0.19,
            -78.47,
            "Entrega",
            RoutePointOperation.Delivery,
            evidence: [image, note]);
        var destination = RoutePoint.Create(-0.20, -78.48, "Destino");
        var route = RouteE.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ruta QA",
            origin,
            destination,
            [origin, stop, destination],
            null,
            null,
            null);

        var listDto = Assert.Single(RouteApplicationMapper.ToDtoList([route]));
        var listEvidence = listDto.Points[1].Evidence;

        Assert.Equal(2, listEvidence.Count);
        Assert.Null(listEvidence[0].Content);
        Assert.Equal("image", listEvidence[0].Type);
        Assert.Equal("Foto de entrega", listEvidence[0].Description);
        Assert.Equal("Entregado", listEvidence[1].Content);

        var detailDto = RouteApplicationMapper.ToDto(route);
        Assert.Equal("data:image/png;base64,AQ==", detailDto.Points[1].Evidence[0].Content);
    }
}
