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

app.MapGet("/employee/by-code", async (string unit, string code, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest(new { ok = false, message = "Unit and employee code are required." });
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
        command.BindByName = true;
        command.CommandText = @"
            SELECT
                E_O.EMP_ID,
                E_O.EMP_CODE,
                E_O.ERP_CODE,
                E_O.EMP_NAME,
                E_O.BANG_EMP_NAME,
                E_P.FATHER_NAME,
                E_P.BANG_FATHER_NAME,
                E_P.MOTHER_NAME,
                E_P.BANG_MOTHER_NAME,
                E_P.HUSBAND_NAME,
                E_P.BANG_HUSBAND_NAME,
                E_P.SEX,
                E_P.RELIGION,
                E_P.MARITAL_STATUS,
                E_P.BLOOD_GROUP,
                E_P.DATE_OF_BIRTH,
                E_P.PRESENT_VILL,
                E_P.PRESENT_HOUSE,
                E_P.PRESENT_PS,
                E_P.PRESENT_DIST,
                E_P.BANG_PRESENT_VILL,
                E_P.BANG_PRESENT_POST,
                E_P.BANG_PRESENT_PS,
                E_P.BANG_PRESENT_DIST,
                E_P.PARMANENT_VILL,
                E_P.PARMANENT_HOUSE,
                E_P.PARMANENT_PS,
                E_P.PARMANENT_DIST,
                E_P.BANG_PERMANENT_VILL,
                E_P.BANG_PERMANENT_POST,
                E_P.BANG_PERMANENT_PS,
                E_P.BANG_PERMANENT_DIST,
                E_P.EDUCATION,
                E_P.EMPLOYEMENT,
                E_P.NATIONAL_ID,
                E_O.BENEFICIARY_NAME,
                E_O.BANG_BENEFICIARY_NAME,
                E_O.RELATION_WITH_BENEFICIARY,
                E_P.NOMINEE_CELL_NO,
                E_P.REMARKS,
                NVL(E_O.TRANSPORT,'N') TRANSPORT,
                E_O.DATE_OF_JOINING,
                E_O.DESIGNATION_ID,
                DESG.DESIGNATION_NAME,
                DESG.GRADE,
                E_O.UNIT_ID,
                UNI.UNIT_NAME,
                E_O.EMP_CATEGORY_ID,
                EC.EMP_CATEGORY_NAME,
                E_O.DEPARTMENT_ID,
                DEPT.DEPARTMENT_NAME,
                E_O.SECTION_ID,
                SEC.SECTION_NAME,
                E_O.LINE_ID,
                LIN.LINE_NAME,
                E_O.SHIFT_ID,
                S_I.SHIFT_NAME,
                E_O.FLOOR_ID,
                FL.FLOOR_NAME,
                E_O.EMP_STATUS,
                E_O.RULE_ID,
                SAL_RUL.RULE_NAME,
                SAL_RUL.RULE_BASIC,
                SAL_RUL.RULE_HOUSE_RENT,
                SAL_RUL.RULE_MEDICAL,
                SAL_RUL.RULE_TRANSPORT,
                SAL_RUL.RULE_FOOD,
                NVL(E_O.GROSS,0) GROSS,
                E_O.WEEKEND,
                E_O.PROXIMITY_NO,
                E_O.LICENSE_NO,
                E_P.E_MAIL,
                E_P.CONTACT_NO,
                E_O.ACCOUNT_NO,
                E_O.MOBILE_BANK_ACC_NO,
                NVL(E_O.BANK_ACCOUNT_HOLDER,'N') BANK_ACCOUNT_HOLDER,
                NVL(E_P.CONTRACTUAL,'N') CONTRACTUAL,
                NVL(E_O.OVER_TIME,'N') OVER_TIME,
                NVL(E_O.LUNCH,'N') LUNCH,
                NVL(E_O.TAX_HOLDER,'N') TAX_HOLDER,
                NVL(E_O.RESIGN_GIVEN,'N') RESIGN_GIVEN,
                E_O.CLOSE_DATE,
                E_O.STS_REASONS,
                NVL(E_O.EL_HOLDER,'N') EL_HOLDER,
                NVL(E_O.EL_SEGMENT,'') EL_SEGMENT
            FROM EMP_OFFICIAL E_O
                INNER JOIN EMP_PERSONAL E_P ON E_O.EMP_ID = E_P.EMP_ID
                LEFT JOIN UNIT UNI ON E_O.UNIT_ID = UNI.UNIT_ID
                LEFT JOIN EMP_CATEGORY EC ON E_O.EMP_CATEGORY_ID = EC.EMP_CATEGORY_ID
                LEFT JOIN DEPARTMENT DEPT ON E_O.DEPARTMENT_ID = DEPT.DEPARTMENT_ID
                LEFT JOIN SECTION SEC ON E_O.SECTION_ID = SEC.SECTION_ID
                LEFT JOIN LINE LIN ON E_O.LINE_ID = LIN.LINE_ID
                LEFT JOIN DESIGNATION DESG ON E_O.DESIGNATION_ID = DESG.DESIGNATION_ID
                LEFT JOIN SHIFT_INFO S_I ON E_O.SHIFT_ID = S_I.SHIFT_ID
                LEFT JOIN SALARY_RULE_INFO SAL_RUL ON E_O.RULE_ID = SAL_RUL.RULE_ID
                LEFT JOIN FLOOR FL ON E_O.FLOOR_ID = FL.FLOOR_ID
            WHERE E_O.EMP_CODE = :empCode";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empCode", code.Trim()));

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return Results.NotFound(new { ok = false, message = "Employee not found." });
        }

        double gross = reader["GROSS"] == DBNull.Value ? 0 : Convert.ToDouble(reader["GROSS"]);
        double ruleBasic = reader["RULE_BASIC"] == DBNull.Value ? 0 : Convert.ToDouble(reader["RULE_BASIC"]);
        double ruleHouseRent = reader["RULE_HOUSE_RENT"] == DBNull.Value ? 0 : Convert.ToDouble(reader["RULE_HOUSE_RENT"]);
        double ruleMedical = reader["RULE_MEDICAL"] == DBNull.Value ? 0 : Convert.ToDouble(reader["RULE_MEDICAL"]);
        double ruleTransport = reader["RULE_TRANSPORT"] == DBNull.Value ? 0 : Convert.ToDouble(reader["RULE_TRANSPORT"]);
        double ruleFood = reader["RULE_FOOD"] == DBNull.Value ? 0 : Convert.ToDouble(reader["RULE_FOOD"]);

        double basic = 0;
        if (ruleBasic > 0)
        {
            if (Math.Abs(ruleBasic - 40) < 0.01 || Math.Abs(ruleBasic - 50) < 0.01)
            {
                basic = Math.Round(gross * (ruleBasic / 100.0), 0);
            }
            else
            {
                var allow = ruleMedical + ruleTransport + ruleFood;
                basic = Math.Round((gross - allow) / (1.0 + (ruleBasic / 100.0)), 0);
            }
        }

        string bankHolder = reader["BANK_ACCOUNT_HOLDER"]?.ToString() ?? "N";
        string accountNo = reader["ACCOUNT_NO"] == DBNull.Value ? "" : reader["ACCOUNT_NO"].ToString();
        string mobileAccountNo = reader["MOBILE_BANK_ACC_NO"] == DBNull.Value ? "" : reader["MOBILE_BANK_ACC_NO"].ToString();
        string resolvedAccountNo = bankHolder == "Y"
            ? accountNo
            : bankHolder == "M"
                ? (string.IsNullOrWhiteSpace(mobileAccountNo) ? accountNo : mobileAccountNo)
                : (string.IsNullOrWhiteSpace(accountNo) ? mobileAccountNo : accountNo);

        string payType = bankHolder == "Y" ? "Bank" : bankHolder == "M" ? "Mobile B" : "Cash";

        return Results.Ok(new
        {
            ok = true,
            employee = new
            {
                empCode = reader["EMP_CODE"]?.ToString(),
                erpCode = reader["ERP_CODE"]?.ToString(),
                empName = reader["EMP_NAME"]?.ToString(),
                empNameBang = reader["BANG_EMP_NAME"]?.ToString(),
                fatherName = reader["FATHER_NAME"]?.ToString(),
                fatherNameBang = reader["BANG_FATHER_NAME"]?.ToString(),
                motherName = reader["MOTHER_NAME"]?.ToString(),
                motherNameBang = reader["BANG_MOTHER_NAME"]?.ToString(),
                spouseName = reader["HUSBAND_NAME"]?.ToString(),
                spouseNameBang = reader["BANG_HUSBAND_NAME"]?.ToString(),
                gender = reader["SEX"]?.ToString(),
                religion = reader["RELIGION"]?.ToString(),
                maritalStatus = reader["MARITAL_STATUS"]?.ToString(),
                bloodGroup = reader["BLOOD_GROUP"]?.ToString(),
                birthDate = reader["DATE_OF_BIRTH"] == DBNull.Value ? "" : Convert.ToDateTime(reader["DATE_OF_BIRTH"]).ToString("yyyy-MM-dd"),
                education = reader["EDUCATION"]?.ToString(),
                experience = reader["EMPLOYEMENT"]?.ToString(),
                nationalId = reader["NATIONAL_ID"]?.ToString(),
                nomineeName = reader["BENEFICIARY_NAME"]?.ToString(),
                nomineeBangla = reader["BANG_BENEFICIARY_NAME"]?.ToString(),
                nomineeRelation = reader["RELATION_WITH_BENEFICIARY"]?.ToString(),
                nomineeCell = reader["NOMINEE_CELL_NO"]?.ToString(),
                remarks = reader["REMARKS"]?.ToString(),
                unit = reader["UNIT_NAME"]?.ToString() ?? unitKey,
                category = reader["EMP_CATEGORY_NAME"]?.ToString(),
                department = reader["DEPARTMENT_NAME"]?.ToString(),
                section = reader["SECTION_NAME"]?.ToString(),
                group = reader["LINE_NAME"]?.ToString(),
                designation = reader["DESIGNATION_NAME"]?.ToString(),
                floor = reader["FLOOR_NAME"]?.ToString(),
                workingTime = reader["SHIFT_NAME"]?.ToString(),
                salaryRule = reader["RULE_NAME"]?.ToString(),
                grade = reader["GRADE"]?.ToString(),
                joinDate = reader["DATE_OF_JOINING"] == DBNull.Value ? "" : Convert.ToDateTime(reader["DATE_OF_JOINING"]).ToString("yyyy-MM-dd"),
                status = reader["EMP_STATUS"]?.ToString(),
                closeDate = reader["CLOSE_DATE"] == DBNull.Value ? "" : Convert.ToDateTime(reader["CLOSE_DATE"]).ToString("yyyy-MM-dd"),
                closeReason = reader["STS_REASONS"]?.ToString(),
                weekend = reader["WEEKEND"]?.ToString(),
                proximityNo = reader["PROXIMITY_NO"]?.ToString(),
                gross = gross,
                basic = basic,
                accountNo = resolvedAccountNo,
                payType,
                elSegment = reader["EL_SEGMENT"]?.ToString(),
                elHolder = (reader["EL_HOLDER"]?.ToString() ?? "N") == "Y",
                otHolder = (reader["OVER_TIME"]?.ToString() ?? "N") == "Y",
                quarterHolder = (reader["LUNCH"]?.ToString() ?? "N") == "Y",
                taxHolder = (reader["TAX_HOLDER"]?.ToString() ?? "N") == "Y",
                contractual = (reader["CONTRACTUAL"]?.ToString() ?? "N") == "Y",
                transport = (reader["TRANSPORT"]?.ToString() ?? "N") == "Y",
                resignGiven = (reader["RESIGN_GIVEN"]?.ToString() ?? "N") == "Y",
                cellNo = reader["CONTACT_NO"]?.ToString(),
                email = reader["E_MAIL"]?.ToString(),
                presentVill = reader["PRESENT_VILL"]?.ToString(),
                presentPo = reader["PRESENT_HOUSE"]?.ToString(),
                presentPs = reader["PRESENT_PS"]?.ToString(),
                presentDist = reader["PRESENT_DIST"]?.ToString(),
                presentVillBang = reader["BANG_PRESENT_VILL"]?.ToString(),
                presentPoBang = reader["BANG_PRESENT_POST"]?.ToString(),
                presentPsBang = reader["BANG_PRESENT_PS"]?.ToString(),
                presentDistBang = reader["BANG_PRESENT_DIST"]?.ToString(),
                permanentVill = reader["PARMANENT_VILL"]?.ToString(),
                permanentPo = reader["PARMANENT_HOUSE"]?.ToString(),
                permanentPs = reader["PARMANENT_PS"]?.ToString(),
                permanentDist = reader["PARMANENT_DIST"]?.ToString(),
                permanentVillBang = reader["BANG_PERMANENT_VILL"]?.ToString(),
                permanentPoBang = reader["BANG_PERMANENT_POST"]?.ToString(),
                permanentPsBang = reader["BANG_PERMANENT_PS"]?.ToString(),
                permanentDistBang = reader["BANG_PERMANENT_DIST"]?.ToString(),
                licenseNo = reader["LICENSE_NO"]?.ToString()
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

static string GetImageContentType(byte[] data)
{
    if (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
    {
        return "image/png";
    }
    if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
    {
        return "image/jpeg";
    }
    if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
    {
        return "image/gif";
    }
    if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4D)
    {
        return "image/bmp";
    }
    return "application/octet-stream";
}

static byte[]? ReadOracleBlob(Oracle.ManagedDataAccess.Client.OracleDataReader reader, int ordinal)
{
    if (reader.IsDBNull(ordinal))
    {
        return null;
    }

    using var blob = reader.GetOracleBlob(ordinal);
    if (blob == null || blob.Length <= 0)
    {
        return null;
    }

    if (blob.Length > int.MaxValue)
    {
        throw new InvalidOperationException("BLOB is too large to buffer in memory.");
    }

    var buffer = new byte[blob.Length];
    int offset = 0;
    const int chunkSize = 8192;
    while (offset < buffer.Length)
    {
        int read = blob.Read(buffer, offset, Math.Min(chunkSize, buffer.Length - offset));
        if (read <= 0)
        {
            break;
        }
        offset += read;
    }
    return buffer;
}

static async Task<byte[]?> ReadBlobViaSubstrAsync(
    Oracle.ManagedDataAccess.Client.OracleConnection connection,
    string lengthSql,
    string chunkSql,
    string empCode)
{
    await using var lengthCommand = connection.CreateCommand();
    lengthCommand.BindByName = true;
    lengthCommand.CommandText = lengthSql;
    lengthCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empCode", empCode));
    var lengthResult = await lengthCommand.ExecuteScalarAsync();
    if (lengthResult == null || lengthResult == DBNull.Value)
    {
        return null;
    }

    var length = Convert.ToInt64(lengthResult);
    if (length <= 0)
    {
        return null;
    }

    const int chunkSize = 2000;
    using var buffer = new MemoryStream((int)Math.Min(length, int.MaxValue));
    for (long pos = 1; pos <= length; pos += chunkSize)
    {
        int readSize = (int)Math.Min(chunkSize, length - pos + 1);
        await using var chunkCommand = connection.CreateCommand();
        chunkCommand.BindByName = true;
        chunkCommand.CommandText = chunkSql;
        chunkCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("chunkSize", Oracle.ManagedDataAccess.Client.OracleDbType.Int32, readSize, System.Data.ParameterDirection.Input));
        chunkCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("pos", Oracle.ManagedDataAccess.Client.OracleDbType.Int32, (int)pos, System.Data.ParameterDirection.Input));
        chunkCommand.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empCode", Oracle.ManagedDataAccess.Client.OracleDbType.Varchar2, empCode, System.Data.ParameterDirection.Input));
        var chunk = await chunkCommand.ExecuteScalarAsync();
        if (chunk == null || chunk == DBNull.Value)
        {
            break;
        }

        byte[]? chunkBytes = null;
        if (chunk is byte[] rawBytes)
        {
            chunkBytes = rawBytes;
        }
        else if (chunk is Oracle.ManagedDataAccess.Types.OracleBinary oracleBinary)
        {
            chunkBytes = oracleBinary.Value;
        }

        if (chunkBytes == null || chunkBytes.Length == 0)
        {
            break;
        }

        await buffer.WriteAsync(chunkBytes, 0, chunkBytes.Length);
    }

    return buffer.Length > 0 ? buffer.ToArray() : null;
}

app.MapGet("/employee/photo", async (string unit, string code, IConfiguration configuration, HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest(new { ok = false, message = "Unit and employee code are required." });
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

        var data = await ReadBlobViaSubstrAsync(
            connection,
            @"SELECT DBMS_LOB.GETLENGTH(E_P.EMP_PHOTO) AS PHOTO_LEN
              FROM EMP_OFFICIAL E_O
              INNER JOIN EMP_PERSONAL E_P ON E_O.EMP_ID = E_P.EMP_ID
              WHERE E_O.EMP_CODE = :empCode",
            @"SELECT DBMS_LOB.SUBSTR(E_P.EMP_PHOTO, :chunkSize, :pos)
              FROM EMP_OFFICIAL E_O
              INNER JOIN EMP_PERSONAL E_P ON E_O.EMP_ID = E_P.EMP_ID
              WHERE E_O.EMP_CODE = :empCode",
            code.Trim());
        if (data == null || data.Length == 0)
        {
            return Results.NotFound(new { ok = false, message = "Photo not found." });
        }

        var contentType = GetImageContentType(data);
        var format = httpContext.Request.Query["format"].ToString();
        if (string.Equals(format, "base64", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok(new { ok = true, contentType, base64 = Convert.ToBase64String(data) });
        }

        return Results.File(data, contentType);
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/employee/signature", async (string unit, string code, IConfiguration configuration, HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest(new { ok = false, message = "Unit and employee code are required." });
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

        var data = await ReadBlobViaSubstrAsync(
            connection,
            @"SELECT DBMS_LOB.GETLENGTH(ES.SIGNATURE) AS SIG_LEN
              FROM EMP_OFFICIAL E_O
              INNER JOIN EMP_SIGNATURE ES ON E_O.EMP_ID = ES.EMP_ID
              WHERE E_O.EMP_CODE = :empCode",
            @"SELECT DBMS_LOB.SUBSTR(ES.SIGNATURE, :chunkSize, :pos)
              FROM EMP_OFFICIAL E_O
              INNER JOIN EMP_SIGNATURE ES ON E_O.EMP_ID = ES.EMP_ID
              WHERE E_O.EMP_CODE = :empCode",
            code.Trim());
        if (data == null || data.Length == 0)
        {
            return Results.NotFound(new { ok = false, message = "Signature not found." });
        }

        var contentType = GetImageContentType(data);
        var format = httpContext.Request.Query["format"].ToString();
        if (string.Equals(format, "base64", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok(new { ok = true, contentType, base64 = Convert.ToBase64String(data) });
        }

        return Results.File(data, contentType);
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/employee/photo-length", async (string unit, string code, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(code))
    {
        return Results.BadRequest(new { ok = false, message = "Unit and employee code are required." });
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
        command.BindByName = true;
        command.CommandText = @"
            SELECT DBMS_LOB.GETLENGTH(E_P.EMP_PHOTO) AS PHOTO_LEN
            FROM EMP_OFFICIAL E_O
            INNER JOIN EMP_PERSONAL E_P ON E_O.EMP_ID = E_P.EMP_ID
            WHERE E_O.EMP_CODE = :empCode";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empCode", code.Trim()));

        var result = await command.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
        {
            return Results.Ok(new { ok = true, length = 0 });
        }
        return Results.Ok(new { ok = true, length = Convert.ToInt64(result) });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
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

static string? GetUnitConnection(string unit, IConfiguration configuration)
{
    if (string.IsNullOrWhiteSpace(unit))
    {
        return null;
    }
    var unitKey = unit.Trim().ToUpperInvariant();
    return configuration.GetSection("UnitConnections")[unitKey];
}

static IResult MissingUnitResult(string unit) =>
    Results.BadRequest(new { ok = false, message = string.IsNullOrWhiteSpace(unit) ? "Unit is required." : $"Unknown unit: {unit.Trim().ToUpperInvariant()}." });

app.MapGet("/lookup/units", async (string unit, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT UNIT_ID, UNIT_NAME FROM UNIT ORDER BY UNIT_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader["UNIT_ID"]?.ToString(),
                name = reader["UNIT_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/lookup/categories", async (string unit, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EMP_CATEGORY_ID, EMP_CATEGORY_NAME FROM EMP_CATEGORY ORDER BY EMP_CATEGORY_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader["EMP_CATEGORY_ID"]?.ToString(),
                name = reader["EMP_CATEGORY_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/lookup/departments", async (string unit, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DEPARTMENT_ID, DEPARTMENT_NAME FROM DEPARTMENT ORDER BY DEPARTMENT_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader["DEPARTMENT_ID"]?.ToString(),
                name = reader["DEPARTMENT_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/lookup/designations", async (string unit, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DESIGNATION_ID, DESIGNATION_NAME FROM DESIGNATION ORDER BY DESIGNATION_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader["DESIGNATION_ID"]?.ToString(),
                name = reader["DESIGNATION_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/lookup/lines", async (string unit, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT LINE_ID, LINE_NAME FROM LINE ORDER BY LINE_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader["LINE_ID"]?.ToString(),
                name = reader["LINE_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/lookup/floors", async (string unit, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FLOOR_ID, FLOOR_NAME FROM FLOOR ORDER BY FLOOR_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader["FLOOR_ID"]?.ToString(),
                name = reader["FLOOR_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/lookup/sections", async (string unit, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SECTION_ID, SECTION_NAME FROM SECTION ORDER BY SECTION_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader["SECTION_ID"]?.ToString(),
                name = reader["SECTION_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/lookup/salary-rules", async (string unit, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RULE_ID, RULE_NAME FROM SALARY_RULE_INFO ORDER BY RULE_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                id = reader["RULE_ID"]?.ToString(),
                name = reader["RULE_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();

record LoginRequest(string Unit, string Username, string Password);
