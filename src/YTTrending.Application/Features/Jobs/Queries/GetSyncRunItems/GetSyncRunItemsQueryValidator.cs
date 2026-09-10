using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRunItems;
public sealed class GetSyncRunItemsQueryValidator : AbstractValidator<GetSyncRunItemsQuery>
{
    public GetSyncRunItemsQueryValidator()
    {
        RuleFor(x => x.SyncRunId)
            .GreaterThan(0).WithMessage("SyncRunId must be greater than 0.");
    }
}
