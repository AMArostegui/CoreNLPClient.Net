# CoreNLPClient.Net

## Summary

CoreNLPClient.Net is a C# client for Stanford CoreNLP Java server. You need a fully functional version of both CoreNLP and Java in your computer to run the client. See https://stanfordnlp.github.io/CoreNLP/corenlp-server.html for further information.

According the project documentation, using a client talking to a server instance is the best way to implement an interface to CoreNLP in other programming languages.

There are several client implementations in many programming languages. The official from the Stanford NLP Group is a *Python* project called *Stanza*. https://stanfordnlp.github.io/stanza/index.html

To keep things simple, I try to mimic the *Stanza* interface and implementation whenever possible.

## Compatibility

The library has been developed and tested on **Windows and Linux**, using **.NET Core 3.1**, but should work with older versions although I haven’t tried.

The targeted version has been **CoreNLP 4.0.0** but, again, it should work with previous releases. The server needs **Java 8** to run.

Furthermore, it should also work with **.NET Framework 4.7.2** and above.

## Limitations

The main goal was to be able to properly run at least the main Python example, altough almost all features of the client have been implemented.

However there are known limitations:

* Redirection of *stdout*, *stderr* of Java Server is not implemented
* Parameter *to_words* for methods *tokensregex, semgrex and tregex* in *stanza.server.client* is not implemented
* kwargs parameters for CoreNLPClient (server_id, ssl, status_port,uriContext, strict, key, username, password, blacklist) should work, but have not been tried.

## Example

Two projects are available in the repository. *CoreNLPClient.Net.csproj* is the library itself. With *Tests.sln / Tests.csproj* you can build and run a program to demostrate the library usage.

**Important:** To run the example you either need

* A *CORENLP_HOME* (or *CLASSPATH*) environment variable set, pointing to the folder containing *CoreNLP jar files*. (For Linux users, a Shell Variable won't be enough, you need a full Environment Variable, see [this](https://linuxize.com/post/how-to-set-and-list-environment-variables-in-linux/))
* To explicitly use classpath parameter in CoreNLP constructor

Assuming all dependencies have been satisfied (see compatibility above), Linux users just need to browse to Test folder and type `dotnet run`. Windows users, you can load *Test.sln* in *Visual Studio 2019 Community Edition*

The following is a comparison of an example using Stanza and CoreNlpClient.Net (click to enlarge)

![Example](https://github.com/AMArostegui/CoreNLPClient.Net/blob/master/Example.png)
