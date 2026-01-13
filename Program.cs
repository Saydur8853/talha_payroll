var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/login", async (LoginRequest request, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(request.Unit) ||
        string.IsNullOrWhiteSpace(request.Username) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { ok = false, message = "Missing credentials." });
    }

    var connectionString = configuration.GetSection("UnitConnections")[request.Unit];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = "Unknown unit." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM USERS WHERE USER_NAME = :username AND PASSWORD = :password";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("username", request.Username));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("password", request.Password));

        var result = await command.ExecuteScalarAsync();
        var matches = Convert.ToInt32(result);

        if (matches > 0)
        {
            return Results.Ok(new { ok = true, unit = request.Unit });
        }

        return Results.Json(new { ok = false, message = "Invalid username or password." }, statusCode: StatusCodes.Status401Unauthorized);
    }
    catch
    {
        return Results.Json(new { ok = false, message = "Database error. Please try again later." }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/total-employees", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL";
        var result = await command.ExecuteScalarAsync();
        var totalEmp = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);

        return Results.Ok(new { ok = true, totalEmp });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/active-employees", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Active'";
        var result = await command.ExecuteScalarAsync();
        var activeEmp = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);

        return Results.Ok(new { ok = true, activeEmp });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/inactive-employees", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Inactive'";
        var result = await command.ExecuteScalarAsync();
        var inactiveEmp = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);

        return Results.Ok(new { ok = true, inactiveEmp });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/new-joiners", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    var now = DateTime.Today;
    var monthStart = new DateTime(now.Year, now.Month, 1);
    var label = now.ToString("MMM,yyyy", System.Globalization.CultureInfo.InvariantCulture);

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE DATE_OF_JOINING >= :fromDate";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", monthStart));
        var result = await command.ExecuteScalarAsync();
        var newJoiners = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);

        return Results.Ok(new { ok = true, newJoiners, label });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/close-release-drop", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    var now = DateTime.Today;
    var monthEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var closeCommand = connection.CreateCommand();
        closeCommand.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Close'";
        var closeResult = await closeCommand.ExecuteScalarAsync();
        var closeEmp = closeResult == null || closeResult == DBNull.Value ? 0 : Convert.ToInt32(closeResult);

        var monthStart = new DateTime(now.Year, now.Month, 1);

        await using var resignCommand = connection.CreateCommand();
        resignCommand.BindByName = true;
        resignCommand.CommandText = "SELECT COUNT(*) AS RESIGNED_COUNT FROM EMP_OFFICIAL WHERE EMP_STATUS='Close' AND NVL(RESIGN_GIVEN,'N')='Y' AND CLOSE_DATE >= :fromDate";
        resignCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", monthStart));
        var resignResult = await resignCommand.ExecuteScalarAsync();
        var resignCount = resignResult == null || resignResult == DBNull.Value ? 0 : Convert.ToInt32(resignResult);

        await using var releaseCommand = connection.CreateCommand();
        releaseCommand.BindByName = true;
        releaseCommand.CommandText = "SELECT COUNT(*) AS NOT_RESIGNED_COUNT FROM EMP_OFFICIAL WHERE EMP_STATUS='Close' AND NVL(RESIGN_GIVEN,'N')='N' AND CLOSE_DATE >= :fromDate";
        releaseCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", monthStart));
        var releaseResult = await releaseCommand.ExecuteScalarAsync();
        var releaseTotal = releaseResult == null || releaseResult == DBNull.Value ? 0 : Convert.ToInt32(releaseResult);

        var toDateText = monthEnd.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var fromDateText = monthEnd.AddDays(-10).ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
        await using var dropCommand = connection.CreateCommand();
        dropCommand.CommandText = $@"SELECT COUNT(A_D.EMP_ID) EMP_ID FROM EMP_OFFICIAL E_O,(SELECT EMP_ID, DECODE(STATUS,'P',MAX(ATTD_DATE)) IN_TIME1,STATUS,TRUNC(TO_DATE('{toDateText}','dd-Mon-yyyy') - ADD_MONTHS(DECODE(STATUS,'P',MAX(ATTD_DATE)),TRUNC(MONTHS_BETWEEN(TO_DATE('{toDateText}','dd-Mon-yyyy'),DECODE(STATUS,'P',MAX(ATTD_DATE)))/12)*12 + TRUNC(MOD(MONTHS_BETWEEN(TO_DATE('{toDateText}','dd-Mon-yyyy'),DECODE(STATUS,'P',MAX(ATTD_DATE))),12)))) ABSENT
                        FROM ATTENDANCE_DETAILS WHERE ATTD_DATE BETWEEN TO_DATE('{fromDateText}','dd-Mon-yyyy') AND TO_DATE('{toDateText}','dd-Mon-yyyy') GROUP BY EMP_ID, STATUS) A_D WHERE E_O.EMP_ID=A_D.EMP_ID AND ABSENT >= 10 AND E_O.EMP_STATUS='Active'";
        var dropResult = await dropCommand.ExecuteScalarAsync();
        var dropOn = dropResult == null || dropResult == DBNull.Value ? 0 : Convert.ToInt32(dropResult);

        var label = now.ToString("MMM,yyyy", System.Globalization.CultureInfo.InvariantCulture);
        return Results.Ok(new { ok = true, closeEmp, releaseTotal, resignCount, dropOn, label });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/close-employees", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Close'";
        var result = await command.ExecuteScalarAsync();
        var closeEmp = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);

        return Results.Ok(new { ok = true, closeEmp });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/release-resign", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    var now = DateTime.Today;
    var monthStart = new DateTime(now.Year, now.Month, 1);

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var resignCommand = connection.CreateCommand();
        resignCommand.BindByName = true;
        resignCommand.CommandText = "SELECT COUNT(*) AS RESIGNED_COUNT FROM EMP_OFFICIAL WHERE EMP_STATUS='Close' AND NVL(RESIGN_GIVEN,'N')='Y' AND CLOSE_DATE >= :fromDate";
        resignCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", monthStart));
        var resignResult = await resignCommand.ExecuteScalarAsync();
        var resignCount = resignResult == null || resignResult == DBNull.Value ? 0 : Convert.ToInt32(resignResult);

        await using var releaseCommand = connection.CreateCommand();
        releaseCommand.BindByName = true;
        releaseCommand.CommandText = "SELECT COUNT(*) AS NOT_RESIGNED_COUNT FROM EMP_OFFICIAL WHERE EMP_STATUS='Close' AND NVL(RESIGN_GIVEN,'N')='N' AND CLOSE_DATE >= :fromDate";
        releaseCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", monthStart));
        var releaseResult = await releaseCommand.ExecuteScalarAsync();
        var releaseTotal = releaseResult == null || releaseResult == DBNull.Value ? 0 : Convert.ToInt32(releaseResult);

        var label = now.ToString("MMM,yyyy", System.Globalization.CultureInfo.InvariantCulture);
        return Results.Ok(new { ok = true, releaseTotal, resignCount, label });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/worker-staff-officer", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        async Task<int> QueryIntAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        var totalWorker = await QueryIntAsync(@"SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL E_O,EMP_CATEGORY E_C WHERE EMP_STATUS='Active' AND E_O.EMP_CATEGORY_ID=E_C.EMP_CATEGORY_ID AND UPPER(EMP_CATEGORY_NAME)='WORKER'");
        var totalStaff = await QueryIntAsync(@"SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL E_O,EMP_CATEGORY E_C WHERE EMP_STATUS='Active' AND E_O.EMP_CATEGORY_ID=E_C.EMP_CATEGORY_ID AND UPPER(EMP_CATEGORY_NAME) NOT IN('WORKER','OFFICER')");
        var totalOfficer = await QueryIntAsync(@"SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL E_O,EMP_CATEGORY E_C WHERE EMP_STATUS='Active' AND E_O.EMP_CATEGORY_ID=E_C.EMP_CATEGORY_ID AND UPPER(EMP_CATEGORY_NAME)='OFFICER'");

        return Results.Ok(new { ok = true, totalWorker, totalStaff, totalOfficer });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/gender", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        async Task<int> QueryIntAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        var totalMale = await QueryIntAsync(@"SELECT COUNT(E_O.EMP_ID) EMP_ID FROM EMP_OFFICIAL E_O,EMP_PERSONAL E_P WHERE EMP_STATUS='Active' AND E_O.EMP_ID=E_P.EMP_ID AND UPPER(SEX)='MALE'");
        var totalFemale = await QueryIntAsync(@"SELECT COUNT(E_O.EMP_ID) EMP_ID FROM EMP_OFFICIAL E_O,EMP_PERSONAL E_P WHERE EMP_STATUS='Active' AND E_O.EMP_ID=E_P.EMP_ID AND UPPER(SEX)='FEMALE'");

        return Results.Ok(new { ok = true, totalMale, totalFemale });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/pay-holders", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        async Task<int> QueryIntAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var result = await command.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        var cashPay = await QueryIntAsync("SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Active' AND BANK_ACCOUNT_HOLDER='N'");
        var bankPay = await QueryIntAsync("SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Active' AND BANK_ACCOUNT_HOLDER='Y' AND TAX_HOLDER='N'");
        var mobilePay = await QueryIntAsync("SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Active' AND BANK_ACCOUNT_HOLDER='M' AND TAX_HOLDER='N'");
        var taxHolder = await QueryIntAsync("SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Active' AND TAX_HOLDER='Y'");

        return Results.Ok(new { ok = true, cashPay, bankPay, mobilePay, taxHolder });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/quarter-increment", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    var now = DateTime.Today;
    var monthStart = new DateTime(now.Year, now.Month, 1);
    var monthEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
    var label = now.ToString("MMM,yyyy", System.Globalization.CultureInfo.InvariantCulture);

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var quarterCommand = connection.CreateCommand();
        quarterCommand.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Active' AND LUNCH='Y'";
        var quarterResult = await quarterCommand.ExecuteScalarAsync();
        var quarterHolder = quarterResult == null || quarterResult == DBNull.Value ? 0 : Convert.ToInt32(quarterResult);

        await using var incrementCommand = connection.CreateCommand();
        incrementCommand.BindByName = true;
        incrementCommand.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Active' AND DATE_OF_JOINING BETWEEN :fromDate AND :toDate";
        incrementCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", monthStart.AddYears(-1)));
        incrementCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("toDate", monthEnd.AddYears(-1)));
        var incrementResult = await incrementCommand.ExecuteScalarAsync();
        var increment = incrementResult == null || incrementResult == DBNull.Value ? 0 : Convert.ToInt32(incrementResult);

        return Results.Ok(new { ok = true, quarterHolder, increment, label });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/off-duty", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    var now = DateTime.Today;

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM ATTENDANCE_DETAILS WHERE STATUS='P' AND STATUS2 IN('W','H') AND TRUNC(ATTD_DATE)=:today";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("today", now));
        var result = await command.ExecuteScalarAsync();
        var offDuty = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);

        return Results.Ok(new { ok = true, offDuty });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/overview/leave-maternity", async (string unit, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return Results.BadRequest(new { ok = false, message = "Unit is required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    var now = DateTime.Today;
    var monthStart = new DateTime(now.Year, now.Month, 1);
    var monthEnd = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
    var label = now.ToString("MMM,yyyy", System.Globalization.CultureInfo.InvariantCulture);

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var leaveCommand = connection.CreateCommand();
        leaveCommand.BindByName = true;
        leaveCommand.CommandText = "SELECT COUNT(DISTINCT EMP_ID) EMP_ID,NVL(SUM(GRANT_DAYS),0) GRANT_DAYS FROM LEAVE WHERE FROM_DATE BETWEEN :fromDate AND :toDate";
        leaveCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", monthStart));
        leaveCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("toDate", monthEnd));
        int leaveEmp = 0;
        int leaveDays = 0;
        await using (var reader = await leaveCommand.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                leaveEmp = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                leaveDays = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
            }
        }

        await using var maternityCommand = connection.CreateCommand();
        maternityCommand.CommandText = "SELECT COUNT(EMP_ID) EMP_ID FROM EMP_OFFICIAL WHERE EMP_STATUS='Maternity'";
        var maternityResult = await maternityCommand.ExecuteScalarAsync();
        var maternity = maternityResult == null || maternityResult == DBNull.Value ? 0 : Convert.ToInt32(maternityResult);

        return Results.Ok(new { ok = true, leaveEmp, leaveDays, maternity, label });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();

record LoginRequest(string Unit, string Username, string Password);
