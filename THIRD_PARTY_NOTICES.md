# Third Party Notices

This file covers dependencies included in the shipped application artifact or required to understand the shipped package composition. Test-only dependencies are excluded from the shipped artifact and are not listed here as redistributable package contents.

## Shipped application dependency notices

### NAudio.Asio 2.3.0

Used by the Windows ASIO output backend in the desktop application.

- Project: [NAudio](https://github.com/naudio/NAudio)
- Package: [NAudio.Asio on NuGet](https://www.nuget.org/packages/NAudio.Asio)
- Reported upstream license: MIT

## Runtime prerequisite note

The default packaged release is framework-dependent and targets the Windows .NET 8 Desktop Runtime. That runtime is a system prerequisite and is not bundled into the default zip artifact produced by this repository.

## Project status note

These notices do not grant redistribution rights for this repository or its packaged output. See [LICENSE.md](LICENSE.md) and [RELEASE_STATUS.md](RELEASE_STATUS.md) for the current redistribution status.
