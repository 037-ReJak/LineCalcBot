using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string ACCESS_TOKEN = "YOUR_CHANNEL_ACCESS_TOKEN";
string dbPath = "calc.db";

InitDB();

app.MapPost("/webhook", async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    var json = JsonDocument.Parse(body);
    var ev = json.RootElement.GetProperty("events")[0];

    if (ev.GetProperty("type").GetString() != "message")
        return Results.Ok();

    string user =
        ev.GetProperty("source").GetProperty("userId").GetString()!;
    string replyToken =
        ev.GetProperty("replyToken").GetString()!;
    string text =
        ev.GetProperty("message").GetProperty("text").GetString()!;

    string result = HandleInput(user, text);

    await Reply(replyToken, result);
    return Results.Ok();
});

app.Run();


// =======================
// 入力処理
// =======================
string HandleInput(string user,string input)
{
    if(input.StartsWith("/pi"))
    {
        int d=10;
        var sp=input.Split(' ');
        if(sp.Length>1) int.TryParse(sp[1],out d);
        return CalculatePi(d);
    }

    if(input=="/history")
        return GetHistory(user);

    if(LooksLikeMath(input))
        return EvaluateAndStore(user,input);

    return "式を入力してください";
}

bool LooksLikeMath(string t)=>
    Regex.IsMatch(t,@"[0-9π√∛+\-*/^()]");


// =======================
// 数式評価
// =======================
string EvaluateAndStore(string user,string expr)
{
    double result = Evaluate(expr);
    string output=Math.Round(result,10).ToString();

    SaveHistory(user,expr,output);
    return output;
}

double Evaluate(string expr)
{
    expr = Normalize(expr);
    var tokens = Tokenize(expr);
    var rpn = ToRPN(tokens);
    return EvalRPN(rpn);
}

string Normalize(string s)=>
    s.Replace("×","*")
     .Replace("÷","/")
     .Replace("π",Math.PI.ToString())
     .Replace("√","sqrt")
     .Replace("∛","cbrt")
     .Replace(" ","");


// =======================
// Tokenize
// =======================
List<string> Tokenize(string expr)
{
    var m=Regex.Matches(expr,
@"sqrt|cbrt|sin|cos|tan|log|ln|\d+(\.\d+)?|[+\-*/^()]");

    return m.Select(x=>x.Value).ToList();
}


// =======================
// Shunting-yard
// =======================
int Prec(string op)=>op switch
{
"+" or "-" =>1,
"*" or "/" =>2,
"^"=>3,
_=>4
};

bool IsFunc(string t)=>
t is "sqrt" or "cbrt" or
"sin" or "cos" or "tan" or
"log" or "ln";

List<string> ToRPN(List<string> tokens)
{
    var output=new List<string>();
    var stack=new Stack<string>();

    foreach(var t in tokens)
    {
        if(double.TryParse(t,out _))
            output.Add(t);

        else if(IsFunc(t))
            stack.Push(t);

        else if("+-*/^".Contains(t))
        {
            while(stack.Count>0 &&
            Prec(stack.Peek())>=Prec(t))
                output.Add(stack.Pop());

            stack.Push(t);
        }
        else if(t=="(") stack.Push(t);

        else if(t==")")
        {
            while(stack.Peek()!="(")
                output.Add(stack.Pop());
            stack.Pop();

            if(stack.Count>0&&IsFunc(stack.Peek()))
                output.Add(stack.Pop());
        }
    }

    while(stack.Count>0)
        output.Add(stack.Pop());

    return output;
}


// =======================
// RPN計算
// =======================
double EvalRPN(List<string> rpn)
{
    var s=new Stack<double>();

    foreach(var t in rpn)
    {
        if(double.TryParse(t,out double n))
            s.Push(n);

        else if(t=="sqrt") s.Push(Math.Sqrt(s.Pop()));
        else if(t=="cbrt") s.Push(Math.Pow(s.Pop(),1.0/3));
        else if(t=="sin") s.Push(Math.Sin(s.Pop()*Math.PI/180));
        else if(t=="cos") s.Push(Math.Cos(s.Pop()*Math.PI/180));
        else if(t=="tan") s.Push(Math.Tan(s.Pop()*Math.PI/180));
        else if(t=="log") s.Push(Math.Log10(s.Pop()));
        else if(t=="ln") s.Push(Math.Log(s.Pop()));
        else
        {
            double b=s.Pop();
            double a=s.Pop();

            s.Push(t switch{
                "+"=>a+b,
                "-"=>a-b,
                "*"=>a*b,
                "/"=>a/b,
                "^"=>Math.Pow(a,b),
                _=>0});
        }
    }
    return s.Pop();
}


// =======================
// π計算
// =======================
string CalculatePi(int digits)
{
    decimal a=1m;
    decimal b=1m/(decimal)Math.Sqrt(2);
    decimal t=0.25m;
    decimal p=1m;

    for(int i=0;i<6;i++)
    {
        decimal an=(a+b)/2;
        decimal bn=(decimal)Math.Sqrt((double)(a*b));
        t-=p*(a-an)*(a-an);
        a=an;b=bn;p*=2;
    }

    decimal pi=(a+b)*(a+b)/(4*t);
    return Math.Round(pi,digits).ToString();
}


// =======================
// SQLite
// =======================
void InitDB()
{
    using var con=new SqliteConnection($"Data Source={dbPath}");
    con.Open();

    var cmd=con.CreateCommand();
    cmd.CommandText=
"CREATE TABLE IF NOT EXISTS history(user TEXT,expr TEXT,result TEXT)";
    cmd.ExecuteNonQuery();
}

void SaveHistory(string u,string e,string r)
{
    using var con=new SqliteConnection($"Data Source={dbPath}");
    con.Open();

    var cmd=con.CreateCommand();
    cmd.CommandText=
"INSERT INTO history VALUES($u,$e,$r)";
    cmd.Parameters.AddWithValue("$u",u);
    cmd.Parameters.AddWithValue("$e",e);
    cmd.Parameters.AddWithValue("$r",r);
    cmd.ExecuteNonQuery();
}

string GetHistory(string u)
{
    using var con=new SqliteConnection($"Data Source={dbPath}");
    con.Open();

    var cmd=con.CreateCommand();
    cmd.CommandText=
"SELECT expr,result FROM history WHERE user=$u ORDER BY rowid DESC LIMIT 10";
    cmd.Parameters.AddWithValue("$u",u);

    using var r=cmd.ExecuteReader();
    var list=new List<string>();

    while(r.Read())
        list.Add($"{r.GetString(0)} = {r.GetString(1)}");

    return list.Count==0?"履歴なし":string.Join("\n",list);
}


// =======================
// LINE返信
// =======================
async Task Reply(string token,string message)
{
    using var client=new HttpClient();

    var payload=new{
        replyToken=token,
        messages=new[]{new{type="text",text=message}}
    };

    var json=JsonSerializer.Serialize(payload);

    var req=new HttpRequestMessage(
        HttpMethod.Post,
        "https://api.line.me/v2/bot/message/reply");

    req.Headers.Add("Authorization",$"Bearer {ACCESS_TOKEN}");
    req.Content=new StringContent(json,Encoding.UTF8,"application/json");

    await client.SendAsync(req);
}