#pragma warning disable 414, 168, 659
// ReSharper disable InconsistentNaming
namespace Iodx.Generated {
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using static TokenType;

    internal class ParseException : Exception {
        public Parser Parser {
            get;
            private set;
        }

        public Token Token {
            get;
            private set;
        }

        public HashSet<TokenType>Expected {
            get;
            private set;
        }

        private IList<NonTerminalCall>callStack;

        public ParseException(Parser parser, HashSet<TokenType>expected) : this(parser, null, null) {
        }

        public ParseException(Parser parser, string message, Token token = null, HashSet<TokenType>expected = null) : base(message) {
            Parser = parser;
            if (expected == null) expected = new HashSet<TokenType>();
            if (token == null) {
                token = parser.LastConsumedToken;
                if ((token != null) && (token.Next != null)) {
                    token = token.Next;
                }
            }
            Token = token;
            Expected = expected;
            callStack = new List<NonTerminalCall>(parser.ParsingStack);
        }

        public ParseException(string message) : base(message) {
            // TODO REVISIT - this is only here because CTL.ccc
            // throws with this signature
        }

        public override String ToString() {
            var oneOf = (Expected.Count == 1) ? "" : "one of ";
            var e = new List<TokenType>(Expected);
            var parts = e.ConvertAll<string>(e => e.ToString());
            var s = string.Join(", ", parts.ToArray());
            return$"{Message}\nUnexpected {Token} ({Token.Type}) at {Token.Location}: expected {oneOf}{s}";
        }
    }

    //
    // Class that represents entering a grammar production
    //
    internal class NonTerminalCall {
        public Parser Parser {
            get;
            private set;
        }

        public string SourceFile {
            get;
            private set;
        }

        public string ProductionName {
            get;
            private set;
        }

        public uint Line {
            get;
            private set;
        }

        public uint Column {
            get;
            private set;
        }

        // REVISIT: Node.NodeType when tree building?
        internal NonTerminalCall(Parser parser, string fileName, string productionName, uint line, uint column) {
            Parser = parser;
            SourceFile = fileName;
            ProductionName = productionName;
            Line = line;
            Column = column;
        }

        /*
        private (string productionName, string sourceFile, uint line) CreateStackTraceElement() {
            return (ProductionName, SourceFile, Line);
        }
 */
    }

    internal class ParseState {
        public Parser Parser {
            get;
            private set;
        }

        public Token LastConsumed {
            get;
            private set;
        }

        public IList<NonTerminalCall>ParsingStack {
            get;
            private set;
        }

        internal ParseState(Parser parser) {
            Parser = parser;
            LastConsumed = parser.LastConsumedToken;
            ParsingStack = new List<NonTerminalCall>(parser.ParsingStack);
        }
    }

    internal class Parser {
        private const uint UNLIMITED = (1U << 31) - 1;

        public string InputSource {
            get;
            private set;
        }

        public Token LastConsumedToken {
            get;
            private set;
        }

        private Token currentLookaheadToken;

        public bool ScanToEnd {
            get;
            private set;
        }

        internal IList<NonTerminalCall>ParsingStack {
            get;
            private set;
        }

        = new List<NonTerminalCall>();

        internal readonly Lexer tokenSource;
        private readonly bool _legacyGlitchyLookahead = false;

        // This property is for testing only
        public bool LegacyGlitchyLookahead {
            get {
                return _legacyGlitchyLookahead;
            }
        }

        private TokenType ? _nextTokenType;
        private uint _remainingLookahead;
        private bool _hitFailure;
        private string _currentlyParsedProduction;
        private string _currentLookaheadProduction;
        private readonly IList<NonTerminalCall>_lookaheadStack = new List<NonTerminalCall>();
        private readonly IList<ParseState>_parseStateStack = new List<ParseState>();

        public Parser(string inputSource) {
            InputSource = inputSource;
            tokenSource = new Lexer(inputSource);
            LastConsumedToken = Token.NewToken(TokenType.DUMMY, tokenSource, 0, 0);
            LastConsumedToken.TokenSource = tokenSource;
        }

        public bool IsTolerant {
            get {
                return false;
            }
        }

        private void PushLastTokenBack() {
            LastConsumedToken = LastConsumedToken.PreviousToken;
        }

        private void StashParseState() {
            _parseStateStack.Add(new ParseState(this));
        }

        private ParseState PopParseState() {
            return _parseStateStack.Pop();
        }

        private void RestoreStashedParseState() {
            var state = PopParseState();
            if (state.LastConsumed != null) {
                // REVISIT
                LastConsumedToken = state.LastConsumed;
            }
            tokenSource.Reset(LastConsumedToken);
        }

        public bool IsTreeBuildingEnabled {
            get {
                return false;
            }
        }

        internal void PushOntoCallStack(string methodName, string fileName, uint line, uint column) {
            ParsingStack.Add(new NonTerminalCall(this, fileName, methodName, line, column));
        }

        internal void PopCallStack() {
            var ntc = ParsingStack.Pop();
            _currentlyParsedProduction = ntc.ProductionName;
        }

        internal void RestoreCallStack(int prevSize) {
            while (ParsingStack.Count > prevSize) {
                PopCallStack();
            }
        }

        // If the next token is cached, it returns that
        // Otherwise, it goes to the lexer.
        private Token NextToken(Token tok) {
            Token result = tokenSource.GetNextToken(tok);
            while (result.IsUnparsed) {
                result = tokenSource.GetNextToken(result);
            }
            _nextTokenType = null;
            return result;
        }

        internal Token GetNextToken() {
            return GetToken(1);
        }

        /**
        * If we are in a lookahead, it looks ahead/behind from the currentLookaheadToken
        * Otherwise, it is the lastConsumedToken
        */
        public Token GetToken(int index) {
            var t = (currentLookaheadToken == null) ? LastConsumedToken : currentLookaheadToken;
            for (var i = 0; i < index; i++) {
                t = NextToken(t);
            }
            for (var i = 0; i > index; i--) {
                t = t.PreviousToken;
                if (t == null) break;
            }
            return t;
        }

        internal string TokenImage(int n) {
            return GetToken(n).ToString();
        }

        internal TokenType GetTokenType(int n) {
            return GetToken(n).Type;
        }

        internal bool CheckNextTokenImage(string img, params string[] additionalImages) {
            var nextImage = TokenImage(1);
            if (nextImage.Equals(img)) {
                return true;
            }
            foreach (var ai in additionalImages) {
                if (nextImage.Equals(ai)) {
                    return true;
                }
            }
            return false;
        }

        internal bool CheckNextTokenType(TokenType tt, params TokenType[] additionalTypes) {
            var nextType = GetToken(1).Type;
            if (nextType == tt) {
                return true;
            }
            foreach (var at in additionalTypes) {
                if (nextType == at) {
                    return true;
                }
            }
            return false;
        }

        internal TokenType NextTokenType {
            get {
                if (_nextTokenType == null) {
                    _nextTokenType = NextToken(LastConsumedToken).Type;
                }
                return _nextTokenType.Value;
            }
        }

        internal void UncacheTokens() {
            tokenSource.UncacheTokens(GetToken(0));
        }

        internal bool ActivateTokenTypes(TokenType type, params TokenType[] types) {
            var result = false;
            var att = tokenSource.ActiveTokenTypes;
            if (!att.Contains(type)) {
                result = true;
                att.Add(type);
            }
            foreach (var tt in types) {
                if (!att.Contains(tt)) {
                    result = true;
                    att.Add(tt);
                }
            }
            tokenSource.Reset(GetToken(0));
            _nextTokenType = null;
            return result;
        }

        internal bool DeactivateTokenTypes(TokenType type, params TokenType[] types) {
            var result = false;
            var att = tokenSource.ActiveTokenTypes;
            if (att.Contains(type)) {
                result = true;
                att.Remove(type);
            }
            foreach (var tt in types) {
                if (att.Contains(tt)) {
                    result = true;
                    att.Remove(tt);
                }
            }
            tokenSource.Reset(GetToken(0));
            _nextTokenType = null;
            return result;
        }

        private void Fail(string message) {
            if (currentLookaheadToken == null) {
                throw new ParseException(this, message);
            }
            _hitFailure = true;
        }

        /**
        *Are we in the production of the given name, either scanning ahead or parsing?
        */
        private bool IsInProduction(params string[] prodNames) {
            if (_currentlyParsedProduction != null) {
                foreach (var name in prodNames) {
                    if (_currentlyParsedProduction.Equals(name)) return true;
                }
            }
            if (_currentLookaheadProduction != null) {
                foreach (var name in prodNames) {
                    if (_currentLookaheadProduction.Equals(name)) return true;
                }
            }
            var it = new BackwardIterator<NonTerminalCall>(ParsingStack, _lookaheadStack);
            while (it.HasNext()) {
                var ntc = it.Next();
                foreach (var name in prodNames) {
                    if (ntc.ProductionName.Equals(name)) {
                        return true;
                    }
                }
            }
            return false;
        }

        // grammar/common/IodxProductions.inc.ccc:4:1
        public IodxCst ParseparseDocument() {
            _currentlyParsedProduction = "parseDocument";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:4:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:5:1
            IodxCst document;
            // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:8:5
            PushOntoCallStack("parseDocument", "grammar/common/IodxProductions.inc.ccc", 8, 5);
            try {
                document = ParseparseListBody();
            }
            finally {
                PopCallStack();
            }
            // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:9:5
            ConsumeToken(TokenType.EOF);
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:10:5
            return document;
        }

        // grammar/common/IodxProductions.inc.ccc:13:1
        public IodxCst ParseparseListBody() {
            _currentlyParsedProduction = "parseListBody";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:13:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:14:1
            Object start = CstBeginNode();
            Object list;
            // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:18:5
            PushOntoCallStack("parseListBody", "grammar/common/IodxProductions.inc.ccc", 18, 5);
            try {
                list = ParseparseList();
            }
            finally {
                PopCallStack();
            }
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:19:5
            return CstListBody(start, list);
        }

        // grammar/common/IodxProductions.inc.ccc:22:1
        public Object ParseparseList() {
            _currentlyParsedProduction = "parseList";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:22:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:23:1
            Object list = CstNewList();
            Object value;
            // Code for ZeroOrOne specified at grammar/common/IodxProductions.inc.ccc:27:5
            if (TypeMatches(TokenType.WHITE_SPACE, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:27:7
                ConsumeToken(TokenType.WHITE_SPACE);
            }
            // Code for ZeroOrOne specified at grammar/common/IodxProductions.inc.ccc:28:5
            if (HasMatch(first_setΣIodxProductions_inc_cccΣ29Σ7, GetToken(1))) {
                // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:29:7
                PushOntoCallStack("parseList", "grammar/common/IodxProductions.inc.ccc", 29, 7);
                try {
                    value = ParseparseElement();
                }
                finally {
                    PopCallStack();
                }
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:29:28
                CstListAdd(list, value);
                // Code for ZeroOrOne specified at grammar/common/IodxProductions.inc.ccc:30:7
                if (TypeMatches(TokenType.WHITE_SPACE, GetToken(1))) {
                    // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:30:9
                    ConsumeToken(TokenType.WHITE_SPACE);
                }
                // Code for ZeroOrMore specified at grammar/common/IodxProductions.inc.ccc:31:7
                while (true) {
                    if (!(HasMatch(first_setΣIodxProductions_inc_cccΣ32Σ9, GetToken(1)))) break;
                    // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:32:9
                    PushOntoCallStack("parseList", "grammar/common/IodxProductions.inc.ccc", 32, 9);
                    try {
                        value = ParseparseElement();
                    }
                    finally {
                        PopCallStack();
                    }
                    // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:32:30
                    CstListAdd(list, value);
                    // Code for ZeroOrOne specified at grammar/common/IodxProductions.inc.ccc:33:9
                    if (TypeMatches(TokenType.WHITE_SPACE, GetToken(1))) {
                        // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:33:11
                        ConsumeToken(TokenType.WHITE_SPACE);
                    }
                }
            }
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:36:5
            return list;
        }

        // grammar/common/IodxProductions.inc.ccc:39:1
        public IodxCst ParseparseElement() {
            _currentlyParsedProduction = "parseElement";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:39:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:40:1
            Token token;
            IodxCst node;
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:45:5
            token = null;
            node = null;
            if (TypeMatches(TokenType.ANY_LITERAL, GetToken(1))) {
                // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:47:7
                PushOntoCallStack("parseElement", "grammar/common/IodxProductions.inc.ccc", 47, 7);
                try {
                    node = ParseparseIdentifierOrClass();
                }
                finally {
                    PopCallStack();
                }
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:47:37
                return node;
            }
            else if (TypeMatches(TokenType.LEFT_PAREN, GetToken(1))) {
                // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:49:7
                PushOntoCallStack("parseElement", "grammar/common/IodxProductions.inc.ccc", 49, 7);
                try {
                    node = ParseparseUnnamedClass();
                }
                finally {
                    PopCallStack();
                }
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:49:32
                return node;
            }
            else if (TypeMatches(TokenType.COMMENT_SINGLE_LINE, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:51:7
                token = ConsumeToken(TokenType.COMMENT_SINGLE_LINE);
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:51:37
                return CstSingleLineComment(token);
            }
            else if (TypeMatches(TokenType.COMMENT_MULTI_LINE, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:53:7
                token = ConsumeToken(TokenType.COMMENT_MULTI_LINE);
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:53:36
                return CstMultiLineComment(token);
            }
            else if (TypeMatches(TokenType.INTEGER_LITERAL, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:55:7
                token = ConsumeToken(TokenType.INTEGER_LITERAL);
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:55:33
                return CstInteger(token);
            }
            else if (TypeMatches(TokenType.FLOATING_POINT_LITERAL, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:57:7
                token = ConsumeToken(TokenType.FLOATING_POINT_LITERAL);
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:57:40
                return CstFloatingPoint(token);
            }
            else if (TypeMatches(TokenType.ANY_OPERATOR, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:59:7
                token = ConsumeToken(TokenType.ANY_OPERATOR);
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:59:30
                return CstRawToken("ANY_OPERATOR", token);
            }
            else if (TypeMatches(TokenType.ANY_SEPARATOR, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:61:7
                token = ConsumeToken(TokenType.ANY_SEPARATOR);
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:61:31
                return CstRawToken("ANY_SEPARATOR", token);
            }
            else if (TypeMatches(TokenType.STRING_LITERAL_DQ, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:63:7
                token = ConsumeToken(TokenType.STRING_LITERAL_DQ);
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:63:35
                return CstString("STRING_LITERAL_DQ", token);
            }
            else if (TypeMatches(TokenType.STRING_LITERAL_SQ, GetToken(1))) {
                // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:65:7
                token = ConsumeToken(TokenType.STRING_LITERAL_SQ);
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:65:35
                return CstString("STRING_LITERAL_SQ", token);
            }
            else {
                PushOntoCallStack("parseElement", "grammar/common/IodxProductions.inc.ccc", 47, 7);
                throw new ParseException(this, first_setΣIodxProductions_inc_cccΣ47Σ7);
            }
        }

        // grammar/common/IodxProductions.inc.ccc:70:1
        public IodxCst ParseparseIdentifierOrClass() {
            _currentlyParsedProduction = "parseIdentifierOrClass";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:70:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:71:1
            Token identifier;
            IodxCst nameNode;
            IodxCst leftParenNode;
            IodxCst rightParenNode;
            IodxCst body;
            // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:78:5
            identifier = ConsumeToken(TokenType.ANY_LITERAL);
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:78:32
            nameNode = CstIdentifier(identifier);
            // Code for ZeroOrOne specified at grammar/common/IodxProductions.inc.ccc:79:5
            if (TypeMatches(TokenType.LEFT_PAREN, GetToken(1))) {
                // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:80:9
                PushOntoCallStack("parseIdentifierOrClass", "grammar/common/IodxProductions.inc.ccc", 80, 9);
                try {
                    leftParenNode = ParseparseLeftParen();
                }
                finally {
                    PopCallStack();
                }
                // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:81:9
                PushOntoCallStack("parseIdentifierOrClass", "grammar/common/IodxProductions.inc.ccc", 81, 9);
                try {
                    body = ParseparseListBody();
                }
                finally {
                    PopCallStack();
                }
                // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:82:9
                PushOntoCallStack("parseIdentifierOrClass", "grammar/common/IodxProductions.inc.ccc", 82, 9);
                try {
                    rightParenNode = ParseparseRightParen();
                }
                finally {
                    PopCallStack();
                }
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:83:9
                return CstClass(nameNode, leftParenNode, body, rightParenNode);
            }
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:85:5
            return nameNode;
        }

        // grammar/common/IodxProductions.inc.ccc:88:1
        public IodxCst ParseparseLeftParen() {
            _currentlyParsedProduction = "parseLeftParen";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:88:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:89:1
            Token token;
            // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:92:5
            token = ConsumeToken(TokenType.LEFT_PAREN);
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:92:26
            return CstStructuralToken("LEFT_PAREN", token);
        }

        // grammar/common/IodxProductions.inc.ccc:95:1
        public IodxCst ParseparseRightParen() {
            _currentlyParsedProduction = "parseRightParen";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:95:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:96:1
            Token token;
            // Code for Terminal specified at grammar/common/IodxProductions.inc.ccc:99:5
            token = ConsumeToken(TokenType.RIGHT_PAREN);
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:99:27
            return CstStructuralToken("RIGHT_PAREN", token);
        }

        // grammar/common/IodxProductions.inc.ccc:103:1
        public IodxCst ParseparseUnnamedClass() {
            _currentlyParsedProduction = "parseUnnamedClass";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:103:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:104:1
            IodxCst leftParenNode;
            IodxCst rightParenNode;
            IodxCst body;
            // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:109:5
            PushOntoCallStack("parseUnnamedClass", "grammar/common/IodxProductions.inc.ccc", 109, 5);
            try {
                leftParenNode = ParseparseLeftParen();
            }
            finally {
                PopCallStack();
            }
            // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:110:5
            PushOntoCallStack("parseUnnamedClass", "grammar/common/IodxProductions.inc.ccc", 110, 5);
            try {
                body = ParseparseListBody();
            }
            finally {
                PopCallStack();
            }
            // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:111:5
            PushOntoCallStack("parseUnnamedClass", "grammar/common/IodxProductions.inc.ccc", 111, 5);
            try {
                rightParenNode = ParseparseRightParen();
            }
            finally {
                PopCallStack();
            }
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:112:5
            return CstClass(null, leftParenNode, body, rightParenNode);
        }

        // grammar/common/IodxProductions.inc.ccc:116:1
        public IodxCst ParseparseClass() {
            _currentlyParsedProduction = "parseClass";
            // Code for BNFProduction specified at grammar/common/IodxProductions.inc.ccc:116:1
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:117:1
            IodxCst node;
            // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:121:5
            node = null;
            if (TypeMatches(TokenType.ANY_LITERAL, GetToken(1))) {
                // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:123:7
                PushOntoCallStack("parseClass", "grammar/common/IodxProductions.inc.ccc", 123, 7);
                try {
                    node = ParseparseIdentifierOrClass();
                }
                finally {
                    PopCallStack();
                }
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:123:37
                return CstRequireClass(node);
            }
            else if (TypeMatches(TokenType.LEFT_PAREN, GetToken(1))) {
                // Code for NonTerminal specified at grammar/common/IodxProductions.inc.ccc:125:7
                PushOntoCallStack("parseClass", "grammar/common/IodxProductions.inc.ccc", 125, 7);
                try {
                    node = ParseparseUnnamedClass();
                }
                finally {
                    PopCallStack();
                }
                // Code for CodeBlock specified at grammar/common/IodxProductions.inc.ccc:125:32
                return node;
            }
            else {
                PushOntoCallStack("parseClass", "grammar/common/IodxProductions.inc.ccc", 123, 7);
                throw new ParseException(this, first_setΣIodxProductions_inc_cccΣ123Σ7);
            }
        }

        private static readonly HashSet<TokenType>first_setΣIodxProductions_inc_cccΣ29Σ7 = Utils.GetOrMakeSet(TokenType.LEFT_PAREN, TokenType.COMMENT_SINGLE_LINE, TokenType.COMMENT_MULTI_LINE, TokenType.INTEGER_LITERAL, TokenType.FLOATING_POINT_LITERAL, TokenType.ANY_LITERAL, TokenType.ANY_OPERATOR, TokenType.ANY_SEPARATOR, TokenType.STRING_LITERAL_DQ, TokenType.STRING_LITERAL_SQ);
        private static readonly HashSet<TokenType>first_setΣIodxProductions_inc_cccΣ32Σ9 = Utils.GetOrMakeSet(TokenType.LEFT_PAREN, TokenType.COMMENT_SINGLE_LINE, TokenType.COMMENT_MULTI_LINE, TokenType.INTEGER_LITERAL, TokenType.FLOATING_POINT_LITERAL, TokenType.ANY_LITERAL, TokenType.ANY_OPERATOR, TokenType.ANY_SEPARATOR, TokenType.STRING_LITERAL_DQ, TokenType.STRING_LITERAL_SQ);
        private static readonly HashSet<TokenType>first_setΣIodxProductions_inc_cccΣ47Σ7 = Utils.GetOrMakeSet(TokenType.LEFT_PAREN, TokenType.COMMENT_SINGLE_LINE, TokenType.COMMENT_MULTI_LINE, TokenType.INTEGER_LITERAL, TokenType.FLOATING_POINT_LITERAL, TokenType.ANY_LITERAL, TokenType.ANY_OPERATOR, TokenType.ANY_SEPARATOR, TokenType.STRING_LITERAL_DQ, TokenType.STRING_LITERAL_SQ);
        private static readonly HashSet<TokenType>first_setΣIodxProductions_inc_cccΣ123Σ7 = Utils.GetOrMakeSet(TokenType.LEFT_PAREN, TokenType.ANY_LITERAL);

        internal bool ScanToken(params TokenType[] types) {
            Token peekedToken = NextToken(currentLookaheadToken);
            bool foundMatch = false;
            foreach (TokenType tt in types) {
                if (TypeMatches(tt, peekedToken)) {
                    foundMatch = true;
                    break;
                }
            }
            if (!foundMatch) return false;
            _remainingLookahead--;
            currentLookaheadToken = peekedToken;
            return true;
        }

        internal bool ScanToken(HashSet<TokenType>types) {
            Token peekedToken = NextToken(currentLookaheadToken);
            if (!HasMatch(types, peekedToken)) {
                return false;
            }
            _remainingLookahead--;
            currentLookaheadToken = peekedToken;
            return true;
        }

        internal bool TypeMatches(TokenType type, Token tok) {
            return tok.Type == type;
        }

        internal bool HasMatch(HashSet<TokenType>types, Token tok) {
            return types.Contains(tok.Type);
        }

        private void PushOntoLookaheadStack(string methodName, string fileName, uint line, uint column) {
            _lookaheadStack.Add(new NonTerminalCall(this, fileName, methodName, line, column));
        }

        private void PopLookaheadStack() {
            var ntc = _lookaheadStack.Pop();
            _currentLookaheadProduction = ntc.ProductionName;
        }

        private Token ConsumeToken(TokenType expectedType) {
            var oldToken = LastConsumedToken;
            var nextToken = NextToken(LastConsumedToken);
            if (nextToken.Type != expectedType) {
                nextToken = HandleUnexpectedTokenType(expectedType, nextToken);
            }
            LastConsumedToken = nextToken;
            _nextTokenType = null;
            return LastConsumedToken;
        }

        private Token HandleUnexpectedTokenType(TokenType expectedType, Token nextToken) {
            throw new ParseException(this, null, nextToken, Utils.EnumSet(expectedType));
        }

        private Object CstBeginNode() {
            return GetToken(0);
        }

        private Object CstNewList() {
            return CstSupport.NewList();
        }

        private void CstListAdd(Object list, Object value) {
            ((ListAdapter<IodxCst>) list).Add(((IodxCst) value));
        }

        private IodxCst CstListBody(Object start, Object children) {
            Token begin = ((Token) start);
            Token end = GetToken(0);
            return CstSupport.ListBody(begin, end, ((ListAdapter<IodxCst>) children));
        }

        private IodxCst CstSingleLineComment(Token token) {
            return CstTokenNode("COMMENT_SINGLE_LINE", token, token.ToString());
        }

        private IodxCst CstMultiLineComment(Token token) {
            return CstTokenNode("COMMENT_MULTI_LINE", token, token.ToString());
        }

        private IodxCst CstInteger(Token token) {
            return CstTokenNode("INTEGER_LITERAL", token, token.ToString());
        }

        private IodxCst CstFloatingPoint(Token token) {
            return CstTokenNode("FLOATING_POINT_LITERAL", token, token.ToString());
        }

        private IodxCst CstRawToken(String type, Token token) {
            return CstTokenNode(type, token, token.ToString());
        }

        private IodxCst CstStructuralToken(String type, Token token) {
            return CstTokenNode(type, token, null);
        }

        private IodxCst CstString(String type, Token token) {
            return CstTokenNode(type, token, token.ToString());
        }

        private IodxCst CstIdentifier(Token token) {
            return CstTokenNode("ANY_LITERAL", token, token.ToString());
        }

        private IodxCst CstClass(IodxCst name, IodxCst left, IodxCst body, IodxCst right) {
            ListAdapter<IodxCst>children = CstSupport.NewList();
            MapAdapter<String, IodxCst>fields = CstSupport.NewMap();
            IodxCst start = (name == null ? left : name);
            if (name != null) {
                fields.Put("name", name);
                children.Add(name);
            }
            children.Add(left);
            children.Add(body);
            children.Add(right);
            fields.Put("body", body);
            return new IodxCst((name == null ? "UNNAMED_CLASS" : "NAMED_CLASS"), start.BeginLine, start.BeginColumn, right.EndLine, right.EndColumn, start.BeginOffset, right.EndOffset, null, children, fields);
        }

        private IodxCst CstRequireClass(IodxCst node) {
            if (node.Type.Equals("NAMED_CLASS")) {
                return node;
            }
            throw new ParseException("Expected a class, but got: " + node.Type);
        }

        private IodxCst CstTokenNode(String type, Token token, Object value) {
            return new IodxCst(type, token.BeginLine, token.BeginColumn, token.EndLine, token.EndColumn, token.BeginOffset, token.EndOffset, value, null, null);
        }
    }
}
