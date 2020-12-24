using System;
using System.Diagnostics;
using Edu.Stanford.Nlp.Pipeline;
using CoreNLPClientDotNet;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            StanzaExample();
            //AdditionalExamples();
        }

        static void StanzaExample()
        {
            var text = "Chris Manning is a nice person. Chris wrote a simple sentence. He also gives oranges to people.";

            using (var client = new CoreNLPClient(
                startServer: StartServer.TryStart,
                annotators: new string[] { "tokenize", "ssplit", "pos", "lemma", "ner", "parse", "depparse", "coref" },
                timeout: 30000,
                memory: "8G"))
            {
                var ann = (Document)client.Annotate(text);
                var sentence = ann.Sentence[0];

                Console.WriteLine("---");
                Console.WriteLine("Constituency parse of first sentence");
                var constituencyParse = sentence.ParseTree;
                Console.WriteLine(constituencyParse);

                Console.WriteLine("---");
                Console.WriteLine("First subtree of constituency parse");
                Console.WriteLine(constituencyParse.Child[0]);

                Console.WriteLine("---");
                Console.WriteLine("Value of first subtree of constituency parse");
                Console.WriteLine(constituencyParse.Child[0].Value);

                Console.WriteLine("---");
                Console.WriteLine("Dependency parse of first sentence");
                var dependencyParse = sentence.BasicDependencies;
                Console.WriteLine(dependencyParse);

                Console.WriteLine("---");
                Console.WriteLine("First token of first sentence");
                var token = sentence.Token[0];
                Console.WriteLine(token);

                Console.WriteLine("---");
                Console.WriteLine("Part of Speech tag of token");
                Console.WriteLine(token.Pos);

                Console.WriteLine("---");
                Console.WriteLine("Named Entity tag of token");
                Console.WriteLine(token.Ner);

                Console.WriteLine("---");
                Console.WriteLine("First entity mention in sentence");
                Console.WriteLine(sentence.Mentions[0]);

                Console.WriteLine("---");
                Console.WriteLine("Coref chains for the example");
                Console.WriteLine(ann.CorefChain);

                var pattern = "([ner: PERSON]+) /wrote/ /an?/ []{0,3} /sentence|article/";
                var matches = client.TokensRegex(text, pattern);

                Console.WriteLine((int)matches["sentences"].Count); // prints: 3
                Console.WriteLine((int)matches["sentences"][1]["length"]); // prints: 1
                Console.WriteLine((string)matches["sentences"][1]["0"]["text"]);
                Console.WriteLine((string)matches["sentences"][1]["0"]["1"]["text"]);

                pattern = "{word:wrote} >nsubj {}=subject >obj {}=object";
                matches = client.Semgrex(text, pattern);
                Console.WriteLine((int)matches["sentences"].Count); // prints: 3
                Console.WriteLine((int)matches["sentences"][1]["length"]); // prints: 1

                pattern = "NP";
                matches = client.TRegex(text, pattern);
                foreach (var match in matches)
                    Console.WriteLine(match);
                Console.WriteLine(matches["sentences"][1]["1"]["match"]); // prints: "(NP (DT a) (JJ simple) (NN sentence))\n"
            }
        }

        static void AdditionalExamples()
        {
            var text = "Chris Manning is a nice person. Chris wrote a simple sentence. He also gives oranges to people.";
            using (var client = new CoreNLPClient(
                //classPath: "%CLASSPATH%",
                //classPath: @"C:\Users\user\Dev\ProductSort\src\Libs\CoreNLP\stanford-corenlp-4.0.0",
                //args: new string[] {"ssl"},
                //kwargs: JObject.Parse("{ \"username\": \"username\", \"password\": \"password\" }"),
                preLoad: false,
                startServer: StartServer.TryStart,
                annotators: new string[] { "tokenize", "ssplit", "pos", "lemma", "ner", "parse", "depparse", "coref" },
                //properties: JObject.Parse("{\"annotators\": \"tokenize,ssplit,pos,lemma,ner,parse,depparse,coref\", \"tokenize.language\": \"en\"}"),
                //properties: "english",
                //properties: "corenlp_server-stanzaexample.props",
                timeout: 30000,
                memory: "8G"))

            //var text = "José López es una buena persona. José ha escrito una frase simple. También reparte naranjas a la gente.";
            //using (var client = new CoreNLPClient(
            //    properties: "spanish",
            //    timeout: 30000,
            //    memory: "8G"))

            {
                var ann = (Document)client.Annotate(text);
                if (ann == null)
                    return;

                var sentence = ann.Sentence[0];
                Console.WriteLine("Annotate OK");

                var pattern = "([ner: PERSON]+) /wrote/ /an?/ []{0,3} /sentence|article/";
                var matches = client.TokensRegex(text, pattern);
                Console.WriteLine("TokensRegex OK");

                pattern = "{word:wrote} >nsubj {}=subject >dobj {}=object";
                matches = client.Semgrex(text, pattern);
                Console.WriteLine("Semgrex OK");

                pattern = "NP";
                matches = client.TRegex(text, pattern);
                Console.WriteLine("TRegex OK");
            }
        }
    }
}
