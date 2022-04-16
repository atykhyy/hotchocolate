using System;
using System.Collections.Generic;

namespace HotChocolate.Stitching.Types.Attempt1.Wip;

public interface IServiceReference
{
    string Name { get; }
    Uri BaseAddress { get; }
    IEnumerable<IServiceFeature> Features { get; }
}