# Dependency license notes

This repository references third-party packages through manifests and lockfiles; it does not vendor dependency
installations or compiled runtime binaries. The .NET projects use only the .NET SDK and, in tests, xUnit v3
(Apache 2.0).

The browser image references the official Microsoft Playwright image by digest in `runtime/browser/pin.json`, and
the script image pins its own base in `runtime/sandbox/pin.json`. No base-image binaries are vendored here.
Building or distributing those images includes Ubuntu, Node, Chrome for Testing and other components with their
own notices and obligations. Preserve the upstream notices and complete a distribution review before publishing an
image.
