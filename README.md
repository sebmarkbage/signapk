SignApk for .NET
----------------

SignApk for .NET is a port of the Android project's SignApk tool that is used to sign .apk files
before publishing. It's written in C#. It uses the built in System.Security assembly as well as
the external SharpZipLib library.

**Certificates**

Unlike the Java implementation, this implementation uses PKCS #12 certificate files. You can
easily convert a key and certificate created by OpenSSL.

    openssl pkcs12 -export -in certificate.pem -inkey key.pem -out key.p12

**CLI Usage**

    signapk [-w] certificate.p12 password input.apk output.apk

**Programmatic Usage**

    void SignPackage(Stream input, X509Certificate2 certificate, Stream output, bool signWholeFile)
