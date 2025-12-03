using CoralLedger.Application.Common.Interfaces;

namespace CoralLedger.Infrastructure.Services;

public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}
