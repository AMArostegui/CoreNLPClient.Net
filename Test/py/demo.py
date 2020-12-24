from stanza.server import CoreNLPClient, StartServer

from asyncio.streams import start_server

# example text
print('---')
print('input text')
print('')

text = 'Chris Manning is a nice person. Chris wrote a simple sentence. He also gives oranges to people.'

with CoreNLPClient(start_server=StartServer.TRY_START, annotators=['tokenize','ssplit','pos','lemma','ner', 'parse', 'depparse','coref'], timeout=30000, memory='8G') as client:
    ann = client.annotate(text=text)
    sentence = ann.sentence[0] 

    print('---')
    print('constituency parse of first sentence')
    constituency_parse = sentence.parseTree
    print(constituency_parse)

    print('---')
    print('first subtree of constituency parse')
    print(constituency_parse.child[0])
 
    print('---')
    print('value of first subtree of constituency parse')
    print(constituency_parse.child[0].value) 

    print('---')
    print('dependency parse of first sentence')
    dependency_parse = sentence.basicDependencies
    print(dependency_parse) 

    print('---')
    print('first token of first sentence')
    token = sentence.token[0]
    print(token) 

    print('---')
    print('part of speech tag of token')
    token.pos
    print(token.pos) 

    print('---')
    print('named entity tag of token')
    print(token.ner)
 
    print('---')
    print('first entity mention in sentence')
    print(sentence.mentions[0])
 
    print('---')
    print('coref chains for the example')
    print(ann.corefChain)

    # Use tokensregex patterns to find who wrote a sentence.
    pattern = '([ner: PERSON]+) /wrote/ /an?/ []{0,3} /sentence|article/'
    matches = client.tokensregex(text, pattern)
    # sentences contains a list with matches for each sentence.
    print(len(matches["sentences"])) # prints: 3
    # length tells you whether or not there are any matches in this
    print(matches["sentences"][1]["length"]) # prints: 1
    # You can access matches like most regex groups.
    print(matches["sentences"][1]["0"]["text"]) # prints: "Chris wrote a simple sentence"
    print(matches["sentences"][1]["0"]["1"]["text"]) # prints: "Chris"

    pattern = '{word:wrote} >nsubj {}=subject >obj {}=object'
    matches = client.semgrex(text, pattern)
    print(len(matches["sentences"])) # prints: 3
    print(matches["sentences"][1]["length"]) # prints: 1
    print(matches["sentences"][1]["0"]["text"])
    print(matches["sentences"][1]["0"]["$subject"]["text"])
    print(matches["sentences"][1]["0"]["$object"]["text"])
 
    pattern = 'NP'
    matches = client.tregex(text, pattern)
    for match in matches:
        print(match)
    print(matches['sentences'][1]['1']['match']) # prints: "(NP (DT a) (JJ simple) (NN sentence))\n"