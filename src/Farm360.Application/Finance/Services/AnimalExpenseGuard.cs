using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Finance.Services;

/// <summary>
/// An expense charged to one animal must belong to an animal on this farm, dated while it was
/// on the farm. A cost dated on or after its sale/death date would land on an animal that had
/// already left and distort its final profit (the same rule treatments and vaccinations follow).
/// </summary>
public static class AnimalExpenseGuard
{
    public static async Task EnsureAsync(
        IAnimalRepository? animals, Guid? animalId, Guid farmId, DateTime transactionDate, CancellationToken cancellationToken)
    {
        if (animals is null || animalId is not { } id)
            return;

        var animal = await animals.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), id);
        if (animal.FarmId != farmId)
            throw new Farm360.Domain.Exceptions.DomainException($"{animal.Tag.TagId} is not on this farm.");

        animal.EnsureCanRecordOn(DateOnly.FromDateTime(transactionDate), "an expense");
    }
}
