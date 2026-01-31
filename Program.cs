using VisorHR.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IUnitDbContextFactory, UnitDbContextFactory>();

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

        var statusReasonColumn = await ResolveStatusReasonColumnAsync(connection);
        var bangPermanentDistColumn = await ResolveBangPermanentDistColumnAsync(connection);
        var bangPermanentPostColumn = await ResolveBangPermanentPostColumnAsync(connection);
        var bangPermanentPsColumn = await ResolveBangPermanentPsColumnAsync(connection);
        var bangPermanentVillColumn = await ResolveBangPermanentVillColumnAsync(connection);

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = $@"
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
                E_P.PERMANENT_VILL,
                E_P.PERMANENT_HOUSE,
                E_P.PERMANENT_PS,
                E_P.PERMANENT_DIST,
                {bangPermanentVillColumn} BANG_PERMANENT_VILL,
                {bangPermanentPostColumn} BANG_PERMANENT_POST,
                {bangPermanentPsColumn} BANG_PERMANENT_PS,
                {bangPermanentDistColumn} BANG_PERMANENT_DIST,
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
                {statusReasonColumn} STS_REASONS,
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
                empId = reader["EMP_ID"] == DBNull.Value ? "" : reader["EMP_ID"].ToString(),
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
                permanentVill = reader["PERMANENT_VILL"]?.ToString(),
                permanentPo = reader["PERMANENT_HOUSE"]?.ToString(),
                permanentPs = reader["PERMANENT_PS"]?.ToString(),
                permanentDist = reader["PERMANENT_DIST"]?.ToString(),
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

app.MapGet("/employee/leave-balance", async (string unit, string? code, string? empId, string? asOf, IConfiguration configuration) =>
{
    if (string.IsNullOrWhiteSpace(unit) || (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(empId)))
    {
        return Results.BadRequest(new { ok = false, message = "Unit and employee code or ID are required." });
    }

    var unitKey = unit.Trim().ToUpperInvariant();
    var connectionString = configuration.GetSection("UnitConnections")[unitKey];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.BadRequest(new { ok = false, message = $"Unknown unit: {unitKey}." });
    }

    var asOfDate = DateTime.Today;
    if (!string.IsNullOrWhiteSpace(asOf) && DateTime.TryParse(asOf, out var parsedAsOf))
    {
        asOfDate = parsedAsOf.Date;
    }

    var currentYearStart = new DateTime(asOfDate.Year, 1, 1);
    var previousYearStart = currentYearStart.AddYears(-1);
    var previousYearMid = new DateTime(asOfDate.Year - 1, 7, 1);

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        var hasEmpId = !string.IsNullOrWhiteSpace(empId);
        command.CommandText = @"
            SELECT
                14 - NVL(GRANT_SL, 0) SL,
                NVL(
                    CASE
                        WHEN E_O.DATE_OF_JOINING < :currentYearStart
                            THEN 10
                        ELSE ROUND(10 / 365 * (365 - ROUND(MONTHS_BETWEEN(E_O.DATE_OF_JOINING, :currentYearStart2) / 12 * 365 + 1)))
                    END,
                    0
                ) - NVL(GRANT_CL, 0) CL,
                DECODE(
                    NVL(E_O.EL_HOLDER, 'N'),
                    'Y',
                    (
                        CASE
                            WHEN TRUNC(MONTHS_BETWEEN(:asOfDate, E_O.DATE_OF_JOINING) / 12) < 1
                                THEN 0
                            ELSE NVL(ROUND(PRESENT / 18, 0), 0)
                        END
                    ) - NVL(GRANT_EL, 0),
                    0
                ) EL
            FROM EMP_OFFICIAL E_O
            LEFT JOIN (
                SELECT A.EMP_ID, COUNT(A.EMP_ID) PRESENT
                FROM ATTENDANCE_DETAILS A
                JOIN EMP_OFFICIAL O ON O.EMP_ID = A.EMP_ID
                LEFT JOIN (
                    SELECT E.EMP_ID, MAX(E.LAST_COUNTING_DATE + 1) LAST_DATE
                    FROM EARN_LEAVE_PROCESS E
                    WHERE E.LAST_COUNTING_DATE < :currentYearStart3
                    GROUP BY E.EMP_ID
                ) E ON O.EMP_ID = E.EMP_ID
                WHERE A.ATTD_DATE BETWEEN NVL(E.LAST_DATE, O.DATE_OF_JOINING) AND :asOfDate2
                    AND A.STATUS = 'P'
                GROUP BY A.EMP_ID
            ) P ON E_O.EMP_ID = P.EMP_ID
            LEFT JOIN (
                SELECT EMP_ID,
                    SUM(NVL(CL, 0)) GRANT_CL,
                    SUM(NVL(SL, 0)) GRANT_SL,
                    SUM(NVL(EL, 0)) GRANT_EL
                FROM (
                    SELECT EMP_ID,
                        DECODE(TYPE, 'CL', SUM(GRANT_DAYS)) CL,
                        DECODE(UPPER(TYPE), 'ML', SUM(GRANT_DAYS), 'SL', SUM(GRANT_DAYS)) SL,
                        0 EL
                    FROM LEAVE
                    WHERE FROM_DATE >= :currentYearStart4
                    GROUP BY EMP_ID, TYPE
                    UNION ALL
                    SELECT L.EMP_ID,
                        0 CL,
                        0 SL,
                        NVL(DECODE(L.TYPE, 'EL', SUM(L.GRANT_DAYS)), 0) EL
                    FROM LEAVE L
                    JOIN EMP_OFFICIAL E_O2 ON E_O2.EMP_ID = L.EMP_ID
                    WHERE E_O2.EL_SEGMENT = 'July' AND L.FROM_DATE >= :previousYearStart
                    GROUP BY L.EMP_ID, L.TYPE
                    UNION ALL
                    SELECT L.EMP_ID,
                        0 CL,
                        0 SL,
                        NVL(DECODE(L.TYPE, 'EL', SUM(L.GRANT_DAYS)), 0) EL
                    FROM LEAVE L
                    JOIN EMP_OFFICIAL E_O3 ON E_O3.EMP_ID = L.EMP_ID
                    WHERE E_O3.EL_SEGMENT = 'January' AND L.FROM_DATE >= :previousYearMid
                    GROUP BY L.EMP_ID, L.TYPE
                )
                GROUP BY EMP_ID
            ) LV ON E_O.EMP_ID = LV.EMP_ID
            WHERE " + (hasEmpId ? "E_O.EMP_ID = :empId" : "E_O.EMP_CODE = :empCode");

        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("currentYearStart", currentYearStart));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("currentYearStart2", currentYearStart));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("currentYearStart3", currentYearStart));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("currentYearStart4", currentYearStart));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("asOfDate", asOfDate));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("asOfDate2", asOfDate));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("previousYearStart", previousYearStart));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("previousYearMid", previousYearMid));
        if (hasEmpId)
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empId", empId.Trim()));
        }
        else
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empCode", code!.Trim()));
        }

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return Results.Ok(new { ok = true, cl = 0, sl = 0, el = 0 });
        }

        int cl = reader["CL"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CL"]);
        int sl = reader["SL"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SL"]);
        int el = reader["EL"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EL"]);

        return Results.Ok(new { ok = true, cl, sl, el });
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

app.MapGet("/shifts", async (string unit, IConfiguration configuration) =>
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
        command.CommandText = @"SELECT SHIFT_ID, SHIFT_NAME, IN_TIME, OUT_TIME, IN_TIME_FROM, OUT_TIME_FROM, GRACE, GRACE_OUT,
            LUNCE_START, LUNCE_END, IN_TIME_START, OUT_TIME_END, DINER_START, DINER_END, DEFAULT_STATUS, REMARKS
            FROM SHIFT_INFO
            ORDER BY SHIFT_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                shiftId = reader["SHIFT_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SHIFT_ID"]),
                shiftName = reader["SHIFT_NAME"]?.ToString(),
                inTime = reader["IN_TIME"]?.ToString(),
                outTime = reader["OUT_TIME"]?.ToString(),
                inTimeFrom = reader["IN_TIME_FROM"]?.ToString(),
                outTimeFrom = reader["OUT_TIME_FROM"]?.ToString(),
                grace = reader["GRACE"]?.ToString(),
                graceOut = reader["GRACE_OUT"]?.ToString(),
                lunchStart = reader["LUNCE_START"]?.ToString(),
                lunchEnd = reader["LUNCE_END"]?.ToString(),
                inTimeStart = reader["IN_TIME_START"]?.ToString(),
                outTimeEnd = reader["OUT_TIME_END"]?.ToString(),
                dinerStart = reader["DINER_START"]?.ToString(),
                dinerEnd = reader["DINER_END"]?.ToString(),
                defaultStatus = reader["DEFAULT_STATUS"]?.ToString(),
                remarks = reader["REMARKS"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/shifts", async (string unit, ShiftRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.ShiftName) ||
        string.IsNullOrWhiteSpace(request.InTime) ||
        string.IsNullOrWhiteSpace(request.OutTime) ||
        string.IsNullOrWhiteSpace(request.InTimeFrom) ||
        string.IsNullOrWhiteSpace(request.OutTimeFrom) ||
        string.IsNullOrWhiteSpace(request.Grace) ||
        string.IsNullOrWhiteSpace(request.GraceOut) ||
        string.IsNullOrWhiteSpace(request.LunchStart) ||
        string.IsNullOrWhiteSpace(request.LunchEnd))
    {
        return Results.BadRequest(new { ok = false, message = "Required shift fields are missing." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT NVL(MAX(SHIFT_ID), 0) + 1 FROM SHIFT_INFO";
        var idResult = await idCommand.ExecuteScalarAsync();
        var shiftId = idResult == null || idResult == DBNull.Value ? 1 : Convert.ToInt32(idResult);

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"INSERT INTO SHIFT_INFO
            (SHIFT_ID, SHIFT_NAME, IN_TIME, OUT_TIME, IN_TIME_FROM, OUT_TIME_FROM, GRACE, GRACE_OUT,
             LUNCE_START, LUNCE_END, IN_TIME_START, OUT_TIME_END, DINER_START, DINER_END, DEFAULT_STATUS, REMARKS)
            VALUES (:shiftId, :shiftName, :inTime, :outTime, :inTimeFrom, :outTimeFrom, :grace, :graceOut,
                    :lunchStart, :lunchEnd, :inTimeStart, :outTimeEnd, :dinerStart, :dinerEnd, :defaultStatus, :remarks)";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shiftId", shiftId));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shiftName", request.ShiftName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("inTime", request.InTime.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("outTime", request.OutTime.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("inTimeFrom", request.InTimeFrom.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("outTimeFrom", request.OutTimeFrom.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("grace", request.Grace.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("graceOut", request.GraceOut.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("lunchStart", request.LunchStart.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("lunchEnd", request.LunchEnd.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("inTimeStart",
            string.IsNullOrWhiteSpace(request.InTimeStart) ? DBNull.Value : request.InTimeStart.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("outTimeEnd",
            string.IsNullOrWhiteSpace(request.OutTimeEnd) ? DBNull.Value : request.OutTimeEnd.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("dinerStart",
            string.IsNullOrWhiteSpace(request.DinerStart) ? DBNull.Value : request.DinerStart.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("dinerEnd",
            string.IsNullOrWhiteSpace(request.DinerEnd) ? DBNull.Value : request.DinerEnd.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("defaultStatus",
            string.IsNullOrWhiteSpace(request.DefaultStatus) ? DBNull.Value : request.DefaultStatus.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));

        await command.ExecuteNonQueryAsync();

        return Results.Ok(new { ok = true, shiftId });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPut("/shifts/{id:int}", async (string unit, int id, ShiftRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.ShiftName) ||
        string.IsNullOrWhiteSpace(request.InTime) ||
        string.IsNullOrWhiteSpace(request.OutTime) ||
        string.IsNullOrWhiteSpace(request.InTimeFrom) ||
        string.IsNullOrWhiteSpace(request.OutTimeFrom) ||
        string.IsNullOrWhiteSpace(request.Grace) ||
        string.IsNullOrWhiteSpace(request.GraceOut) ||
        string.IsNullOrWhiteSpace(request.LunchStart) ||
        string.IsNullOrWhiteSpace(request.LunchEnd))
    {
        return Results.BadRequest(new { ok = false, message = "Required shift fields are missing." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"UPDATE SHIFT_INFO
            SET SHIFT_NAME = :shiftName,
                IN_TIME = :inTime,
                OUT_TIME = :outTime,
                IN_TIME_FROM = :inTimeFrom,
                OUT_TIME_FROM = :outTimeFrom,
                GRACE = :grace,
                GRACE_OUT = :graceOut,
                LUNCE_START = :lunchStart,
                LUNCE_END = :lunchEnd,
                IN_TIME_START = :inTimeStart,
                OUT_TIME_END = :outTimeEnd,
                DINER_START = :dinerStart,
                DINER_END = :dinerEnd,
                DEFAULT_STATUS = :defaultStatus,
                REMARKS = :remarks,
                UPDATED_DATE = SYSDATE
            WHERE SHIFT_ID = :shiftId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shiftName", request.ShiftName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("inTime", request.InTime.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("outTime", request.OutTime.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("inTimeFrom", request.InTimeFrom.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("outTimeFrom", request.OutTimeFrom.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("grace", request.Grace.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("graceOut", request.GraceOut.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("lunchStart", request.LunchStart.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("lunchEnd", request.LunchEnd.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("inTimeStart",
            string.IsNullOrWhiteSpace(request.InTimeStart) ? DBNull.Value : request.InTimeStart.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("outTimeEnd",
            string.IsNullOrWhiteSpace(request.OutTimeEnd) ? DBNull.Value : request.OutTimeEnd.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("dinerStart",
            string.IsNullOrWhiteSpace(request.DinerStart) ? DBNull.Value : request.DinerStart.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("dinerEnd",
            string.IsNullOrWhiteSpace(request.DinerEnd) ? DBNull.Value : request.DinerEnd.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("defaultStatus",
            string.IsNullOrWhiteSpace(request.DefaultStatus) ? DBNull.Value : request.DefaultStatus.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shiftId", id));

        var updated = await command.ExecuteNonQueryAsync();
        if (updated == 0)
        {
            return Results.NotFound(new { ok = false, message = "Shift not found." });
        }

        return Results.Ok(new { ok = true, shiftId = id });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapDelete("/shifts/{id:int}", async (string unit, int id, IConfiguration configuration) =>
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
        command.BindByName = true;
        command.CommandText = "DELETE FROM SHIFT_INFO WHERE SHIFT_ID = :shiftId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shiftId", id));

        var deleted = await command.ExecuteNonQueryAsync();
        if (deleted == 0)
        {
            return Results.NotFound(new { ok = false, message = "Shift not found." });
        }

        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/sections", async (string unit, IConfiguration configuration) =>
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
        command.CommandText = @"SELECT SECTION_ID, SECTION_NAME, UNIT_ID, REMARKS, SHOW_TOGETHER, BANG_SEC_NAME
            FROM SECTION
            ORDER BY SECTION_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                sectionId = reader["SECTION_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SECTION_ID"]),
                sectionName = reader["SECTION_NAME"]?.ToString(),
                unitId = reader["UNIT_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["UNIT_ID"]),
                remarks = reader["REMARKS"]?.ToString(),
                showTogether = reader["SHOW_TOGETHER"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SHOW_TOGETHER"]),
                bangSecName = reader["BANG_SEC_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/sections", async (string unit, SectionRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.SectionName) || request.UnitId is null)
    {
        return Results.BadRequest(new { ok = false, message = "Section name and unit are required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT NVL(MAX(SECTION_ID), 0) + 1 FROM SECTION";
        var idResult = await idCommand.ExecuteScalarAsync();
        var sectionId = idResult == null || idResult == DBNull.Value ? 1 : Convert.ToInt32(idResult);

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"INSERT INTO SECTION
            (SECTION_ID, SECTION_NAME, UNIT_ID, REMARKS, SHOW_TOGETHER, BANG_SEC_NAME)
            VALUES (:sectionId, :sectionName, :unitId, :remarks, :showTogether, :bangSecName)";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("sectionId", sectionId));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("sectionName", request.SectionName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("unitId", request.UnitId.Value));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("showTogether", request.ShowTogether ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangSecName",
            string.IsNullOrWhiteSpace(request.BangSecName) ? DBNull.Value : request.BangSecName.Trim()));

        await command.ExecuteNonQueryAsync();

        return Results.Ok(new { ok = true, sectionId });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPut("/sections/{id:int}", async (string unit, int id, SectionRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.SectionName) || request.UnitId is null)
    {
        return Results.BadRequest(new { ok = false, message = "Section name and unit are required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"UPDATE SECTION
            SET SECTION_NAME = :sectionName,
                UNIT_ID = :unitId,
                REMARKS = :remarks,
                SHOW_TOGETHER = :showTogether,
                BANG_SEC_NAME = :bangSecName,
                UPDATED_DATE = SYSDATE
            WHERE SECTION_ID = :sectionId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("sectionName", request.SectionName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("unitId", request.UnitId.Value));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("showTogether", request.ShowTogether ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangSecName",
            string.IsNullOrWhiteSpace(request.BangSecName) ? DBNull.Value : request.BangSecName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("sectionId", id));

        var updated = await command.ExecuteNonQueryAsync();
        if (updated == 0)
        {
            return Results.NotFound(new { ok = false, message = "Section not found." });
        }

        return Results.Ok(new { ok = true, sectionId = id });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapDelete("/sections/{id:int}", async (string unit, int id, IConfiguration configuration) =>
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
        command.BindByName = true;
        command.CommandText = "DELETE FROM SECTION WHERE SECTION_ID = :sectionId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("sectionId", id));

        var deleted = await command.ExecuteNonQueryAsync();
        if (deleted == 0)
        {
            return Results.NotFound(new { ok = false, message = "Section not found." });
        }

        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/employee-types", async (string unit, IConfiguration configuration) =>
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
        command.CommandText = @"SELECT EMP_CATEGORY_ID, EMP_CATEGORY_NAME, POSITION_LEVEL, TIFFIN_ALW, BANG_EMP_TYPE_NAME, REMARKS
            FROM EMP_CATEGORY
            ORDER BY EMP_CATEGORY_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                categoryId = reader["EMP_CATEGORY_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EMP_CATEGORY_ID"]),
                categoryName = reader["EMP_CATEGORY_NAME"]?.ToString(),
                positionLevel = reader["POSITION_LEVEL"] == DBNull.Value ? 0 : Convert.ToInt32(reader["POSITION_LEVEL"]),
                tiffinAllowance = reader["TIFFIN_ALW"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TIFFIN_ALW"]),
                bangTypeName = reader["BANG_EMP_TYPE_NAME"]?.ToString(),
                remarks = reader["REMARKS"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/employee-types", async (string unit, EmployeeTypeRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.CategoryName))
    {
        return Results.BadRequest(new { ok = false, message = "Employee type name is required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT NVL(MAX(EMP_CATEGORY_ID), 0) + 1 FROM EMP_CATEGORY";
        var idResult = await idCommand.ExecuteScalarAsync();
        var categoryId = idResult == null || idResult == DBNull.Value ? 1 : Convert.ToInt32(idResult);

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"INSERT INTO EMP_CATEGORY
            (EMP_CATEGORY_ID, EMP_CATEGORY_NAME, POSITION_LEVEL, TIFFIN_ALW, BANG_EMP_TYPE_NAME, REMARKS)
            VALUES (:categoryId, :categoryName, :positionLevel, :tiffinAlw, :bangTypeName, :remarks)";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("categoryId", categoryId));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("categoryName", request.CategoryName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("positionLevel", request.PositionLevel ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("tiffinAlw", request.TiffinAllowance ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangTypeName",
            string.IsNullOrWhiteSpace(request.BangTypeName) ? DBNull.Value : request.BangTypeName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));

        await command.ExecuteNonQueryAsync();

        return Results.Ok(new { ok = true, categoryId });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPut("/employee-types/{id:int}", async (string unit, int id, EmployeeTypeRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.CategoryName))
    {
        return Results.BadRequest(new { ok = false, message = "Employee type name is required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"UPDATE EMP_CATEGORY
            SET EMP_CATEGORY_NAME = :categoryName,
                POSITION_LEVEL = :positionLevel,
                TIFFIN_ALW = :tiffinAlw,
                BANG_EMP_TYPE_NAME = :bangTypeName,
                REMARKS = :remarks,
                UPDATED_DATE = SYSDATE
            WHERE EMP_CATEGORY_ID = :categoryId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("categoryName", request.CategoryName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("positionLevel", request.PositionLevel ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("tiffinAlw", request.TiffinAllowance ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangTypeName",
            string.IsNullOrWhiteSpace(request.BangTypeName) ? DBNull.Value : request.BangTypeName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("categoryId", id));

        var updated = await command.ExecuteNonQueryAsync();
        if (updated == 0)
        {
            return Results.NotFound(new { ok = false, message = "Employee type not found." });
        }

        return Results.Ok(new { ok = true, categoryId = id });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapDelete("/employee-types/{id:int}", async (string unit, int id, IConfiguration configuration) =>
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
        command.BindByName = true;
        command.CommandText = "DELETE FROM EMP_CATEGORY WHERE EMP_CATEGORY_ID = :categoryId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("categoryId", id));

        var deleted = await command.ExecuteNonQueryAsync();
        if (deleted == 0)
        {
            return Results.NotFound(new { ok = false, message = "Employee type not found." });
        }

        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/designations", async (string unit, IConfiguration configuration) =>
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
        command.CommandText = @"SELECT DESIGNATION_ID, DESIGNATION_NAME, GRADE, POS_ID, POSITION, POSITION_PRIORITY,
            APPR_ATTD_BONUS, OFF_DAY_ALW, BANG_GRADE, BANG_DESIGNATION_NAME, REMARKS, DORM_ENTITLE, NON_DORMITORY
            FROM DESIGNATION
            ORDER BY DESIGNATION_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                designationId = reader["DESIGNATION_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DESIGNATION_ID"]),
                designationName = reader["DESIGNATION_NAME"]?.ToString(),
                grade = reader["GRADE"]?.ToString(),
                positionId = reader["POS_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["POS_ID"]),
                position = reader["POSITION"]?.ToString(),
                positionPriority = reader["POSITION_PRIORITY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["POSITION_PRIORITY"]),
                apprAttdBonus = reader["APPR_ATTD_BONUS"] == DBNull.Value ? 0 : Convert.ToInt32(reader["APPR_ATTD_BONUS"]),
                offDayAlw = reader["OFF_DAY_ALW"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OFF_DAY_ALW"]),
                bangGrade = reader["BANG_GRADE"]?.ToString(),
                bangDesignationName = reader["BANG_DESIGNATION_NAME"]?.ToString(),
                remarks = reader["REMARKS"]?.ToString(),
                dormEntitle = reader["DORM_ENTITLE"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DORM_ENTITLE"]),
                nonDormitory = reader["NON_DORMITORY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NON_DORMITORY"])
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/designations", async (string unit, DesignationRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.DesignationName))
    {
        return Results.BadRequest(new { ok = false, message = "Designation name is required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT NVL(MAX(DESIGNATION_ID), 0) + 1 FROM DESIGNATION";
        var idResult = await idCommand.ExecuteScalarAsync();
        var designationId = idResult == null || idResult == DBNull.Value ? 1 : Convert.ToInt32(idResult);

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"INSERT INTO DESIGNATION
            (DESIGNATION_ID, DESIGNATION_NAME, GRADE, POS_ID, POSITION, POSITION_PRIORITY, APPR_ATTD_BONUS, OFF_DAY_ALW,
             BANG_GRADE, BANG_DESIGNATION_NAME, REMARKS, DORM_ENTITLE, NON_DORMITORY)
            VALUES (:designationId, :designationName, :grade, :positionId, :position, :positionPriority, :apprAttdBonus,
                    :offDayAlw, :bangGrade, :bangDesignationName, :remarks, :dormEntitle, :nonDormitory)";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("designationId", designationId));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("designationName", request.DesignationName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("grade",
            string.IsNullOrWhiteSpace(request.Grade) ? DBNull.Value : request.Grade.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("positionId", request.PositionId ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("position",
            string.IsNullOrWhiteSpace(request.Position) ? DBNull.Value : request.Position.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("positionPriority", request.PositionPriority ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("apprAttdBonus", request.ApprAttdBonus ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("offDayAlw", request.OffDayAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangGrade",
            string.IsNullOrWhiteSpace(request.BangGrade) ? DBNull.Value : request.BangGrade.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangDesignationName",
            string.IsNullOrWhiteSpace(request.BangDesignationName) ? DBNull.Value : request.BangDesignationName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("dormEntitle", request.DormEntitle ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("nonDormitory", request.NonDormitory ?? 0));

        await command.ExecuteNonQueryAsync();

        return Results.Ok(new { ok = true, designationId });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPut("/designations/{id:int}", async (string unit, int id, DesignationRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.DesignationName))
    {
        return Results.BadRequest(new { ok = false, message = "Designation name is required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"UPDATE DESIGNATION
            SET DESIGNATION_NAME = :designationName,
                GRADE = :grade,
                POS_ID = :positionId,
                POSITION = :position,
                POSITION_PRIORITY = :positionPriority,
                APPR_ATTD_BONUS = :apprAttdBonus,
                OFF_DAY_ALW = :offDayAlw,
                BANG_GRADE = :bangGrade,
                BANG_DESIGNATION_NAME = :bangDesignationName,
                REMARKS = :remarks,
                DORM_ENTITLE = :dormEntitle,
                NON_DORMITORY = :nonDormitory,
                UPDATED_DATE = SYSDATE
            WHERE DESIGNATION_ID = :designationId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("designationName", request.DesignationName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("grade",
            string.IsNullOrWhiteSpace(request.Grade) ? DBNull.Value : request.Grade.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("positionId", request.PositionId ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("position",
            string.IsNullOrWhiteSpace(request.Position) ? DBNull.Value : request.Position.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("positionPriority", request.PositionPriority ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("apprAttdBonus", request.ApprAttdBonus ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("offDayAlw", request.OffDayAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangGrade",
            string.IsNullOrWhiteSpace(request.BangGrade) ? DBNull.Value : request.BangGrade.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangDesignationName",
            string.IsNullOrWhiteSpace(request.BangDesignationName) ? DBNull.Value : request.BangDesignationName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("dormEntitle", request.DormEntitle ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("nonDormitory", request.NonDormitory ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("designationId", id));

        var updated = await command.ExecuteNonQueryAsync();
        if (updated == 0)
        {
            return Results.NotFound(new { ok = false, message = "Designation not found." });
        }

        return Results.Ok(new { ok = true, designationId = id });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapDelete("/designations/{id:int}", async (string unit, int id, IConfiguration configuration) =>
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
        command.BindByName = true;
        command.CommandText = "DELETE FROM DESIGNATION WHERE DESIGNATION_ID = :designationId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("designationId", id));

        var deleted = await command.ExecuteNonQueryAsync();
        if (deleted == 0)
        {
            return Results.NotFound(new { ok = false, message = "Designation not found." });
        }

        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/salary-rules", async (string unit, IConfiguration configuration) =>
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
        command.CommandText = @"SELECT RULE_ID, RULE_NAME, RULE_BASIC, RULE_HOUSE_RENT, RULE_MEDICAL, RULE_TRANSPORT,
            RULE_FOOD, GET_ATTD_BONUS, MIN_ATTD_BONUS, RULE_DEAR_ALW, ATTD_ALW, OT_ALW, NIGHT_BILL, WASHING_BILL,
            DRIVER_ALW, EXPORT_ALW, IS_DEDUCT, RULE_STATUS, RULE_REMARKS
            FROM SALARY_RULE_INFO
            ORDER BY RULE_NAME";

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                ruleId = reader["RULE_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RULE_ID"]),
                ruleName = reader["RULE_NAME"]?.ToString(),
                ruleBasic = reader["RULE_BASIC"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RULE_BASIC"]),
                ruleHouseRent = reader["RULE_HOUSE_RENT"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RULE_HOUSE_RENT"]),
                ruleMedical = reader["RULE_MEDICAL"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RULE_MEDICAL"]),
                ruleTransport = reader["RULE_TRANSPORT"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RULE_TRANSPORT"]),
                ruleFood = reader["RULE_FOOD"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RULE_FOOD"]),
                getAttdBonus = reader["GET_ATTD_BONUS"] == DBNull.Value ? 0 : Convert.ToInt32(reader["GET_ATTD_BONUS"]),
                minAttdBonus = reader["MIN_ATTD_BONUS"] == DBNull.Value ? 0 : Convert.ToInt32(reader["MIN_ATTD_BONUS"]),
                ruleDearAlw = reader["RULE_DEAR_ALW"] == DBNull.Value ? 0 : Convert.ToInt32(reader["RULE_DEAR_ALW"]),
                attdAlw = reader["ATTD_ALW"] == DBNull.Value ? 0 : Convert.ToInt32(reader["ATTD_ALW"]),
                otAlw = reader["OT_ALW"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OT_ALW"]),
                nightBill = reader["NIGHT_BILL"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NIGHT_BILL"]),
                washingBill = reader["WASHING_BILL"] == DBNull.Value ? 0 : Convert.ToInt32(reader["WASHING_BILL"]),
                driverAlw = reader["DRIVER_ALW"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DRIVER_ALW"]),
                exportAlw = reader["EXPORT_ALW"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EXPORT_ALW"]),
                isDeduct = reader["IS_DEDUCT"]?.ToString(),
                ruleStatus = reader["RULE_STATUS"]?.ToString(),
                ruleRemarks = reader["RULE_REMARKS"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/attendance/count", async (string unit, string? fromDate, string? toDate, string? empCode, int? empId, string? status, int? shiftId, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    var startDate = DateTime.Today;
    if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var parsedFrom))
    {
        startDate = parsedFrom.Date;
    }

    var endDate = startDate;
    if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var parsedTo))
    {
        endDate = parsedTo.Date;
    }

    var endDateExclusive = endDate.AddDays(1);

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;

        var whereClauses = new List<string>
        {
            "A.ATTD_DATE >= :fromDate",
            "A.ATTD_DATE < :toDate"
        };

        if (!string.IsNullOrWhiteSpace(empCode))
        {
            whereClauses.Add("UPPER(E_O.EMP_CODE) = :empCode");
        }

        if (empId.HasValue)
        {
            whereClauses.Add("A.EMP_ID = :empId");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            whereClauses.Add("A.STATUS = :status");
        }

        if (shiftId.HasValue)
        {
            whereClauses.Add("A.SHIFT_ID = :shiftId");
        }

        command.CommandText = $@"
            SELECT COUNT(1)
            FROM ATTENDANCE_DETAILS A
                LEFT JOIN EMP_OFFICIAL E_O ON A.EMP_ID = E_O.EMP_ID
            WHERE {string.Join(" AND ", whereClauses)}";

        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", startDate));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("toDate", endDateExclusive));

        if (!string.IsNullOrWhiteSpace(empCode))
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empCode", empCode.Trim().ToUpperInvariant()));
        }

        if (empId.HasValue)
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empId", empId.Value));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("status", status.Trim()));
        }

        if (shiftId.HasValue)
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shiftId", shiftId.Value));
        }

        var result = await command.ExecuteScalarAsync();
        var total = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);

        return Results.Ok(new { ok = true, total });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/attendance", async (string unit, string? fromDate, string? toDate, string? empCode, int? empId, string? status, int? shiftId, int? page, int? pageSize, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    var startDate = DateTime.Today;
    if (!string.IsNullOrWhiteSpace(fromDate) && DateTime.TryParse(fromDate, out var parsedFrom))
    {
        startDate = parsedFrom.Date;
    }

    var endDate = startDate;
    if (!string.IsNullOrWhiteSpace(toDate) && DateTime.TryParse(toDate, out var parsedTo))
    {
        endDate = parsedTo.Date;
    }

    var endDateExclusive = endDate.AddDays(1);
    var resolvedPage = page.GetValueOrDefault(1);
    var resolvedPageSize = pageSize.GetValueOrDefault(500);
    if (resolvedPage < 1)
    {
        resolvedPage = 1;
    }

    if (resolvedPageSize < 1)
    {
        resolvedPageSize = 500;
    }

    var offset = (resolvedPage - 1) * resolvedPageSize;
    var startRow = offset + 1;
    var endRow = offset + resolvedPageSize;

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;

        var whereClauses = new List<string>
        {
            "A.ATTD_DATE >= :fromDate",
            "A.ATTD_DATE < :toDate"
        };

        if (!string.IsNullOrWhiteSpace(empCode))
        {
            whereClauses.Add("UPPER(E_O.EMP_CODE) = :empCode");
        }

        if (empId.HasValue)
        {
            whereClauses.Add("A.EMP_ID = :empId");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            whereClauses.Add("A.STATUS = :status");
        }

        if (shiftId.HasValue)
        {
            whereClauses.Add("A.SHIFT_ID = :shiftId");
        }

        command.CommandText = $@"
            SELECT *
            FROM (
                SELECT
                    A.ATTD_DATE,
                    A.IN_TIME,
                    A.OUT_TIME,
                    A.STATUS,
                    A.LATE,
                    A.EARLY_OUT,
                    A.OVER_TIME,
                    A.EMP_ID,
                    E_O.EMP_CODE,
                    E_O.EMP_NAME,
                    A.SHIFT_ID,
                    S.SHIFT_NAME,
                    ROW_NUMBER() OVER (ORDER BY A.ATTD_DATE DESC, E_O.EMP_CODE) RN
                FROM ATTENDANCE_DETAILS A
                    LEFT JOIN EMP_OFFICIAL E_O ON A.EMP_ID = E_O.EMP_ID
                    LEFT JOIN SHIFT_INFO S ON A.SHIFT_ID = S.SHIFT_ID
                WHERE {string.Join(" AND ", whereClauses)}
            )
            WHERE RN BETWEEN :startRow AND :endRow
            ORDER BY RN";

        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("fromDate", startDate));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("toDate", endDateExclusive));

        if (!string.IsNullOrWhiteSpace(empCode))
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empCode", empCode.Trim().ToUpperInvariant()));
        }

        if (empId.HasValue)
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("empId", empId.Value));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("status", status.Trim()));
        }

        if (shiftId.HasValue)
        {
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shiftId", shiftId.Value));
        }

        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("startRow", startRow));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("endRow", endRow));

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            var attdDate = reader["ATTD_DATE"] == DBNull.Value ? "" : Convert.ToDateTime(reader["ATTD_DATE"]).ToString("yyyy-MM-dd");
            var inTime = reader["IN_TIME"] == DBNull.Value ? "" : Convert.ToDateTime(reader["IN_TIME"]).ToString("HH:mm");
            var outTime = reader["OUT_TIME"] == DBNull.Value ? "" : Convert.ToDateTime(reader["OUT_TIME"]).ToString("HH:mm");
            items.Add(new
            {
                attdDate,
                empId = reader["EMP_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EMP_ID"]),
                empCode = reader["EMP_CODE"]?.ToString(),
                empName = reader["EMP_NAME"]?.ToString(),
                status = reader["STATUS"]?.ToString(),
                inTime,
                outTime,
                late = reader["LATE"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LATE"]),
                earlyOut = reader["EARLY_OUT"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EARLY_OUT"]),
                overTime = reader["OVER_TIME"] == DBNull.Value ? 0 : Convert.ToInt32(reader["OVER_TIME"]),
                shiftId = reader["SHIFT_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SHIFT_ID"]),
                shiftName = reader["SHIFT_NAME"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/salary-rules", async (string unit, SalaryRuleRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.RuleName))
    {
        return Results.BadRequest(new { ok = false, message = "Rule name is required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT NVL(MAX(RULE_ID), 0) + 1 FROM SALARY_RULE_INFO";
        var idResult = await idCommand.ExecuteScalarAsync();
        var ruleId = idResult == null || idResult == DBNull.Value ? 1 : Convert.ToInt32(idResult);

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"INSERT INTO SALARY_RULE_INFO
            (RULE_ID, RULE_NAME, RULE_BASIC, RULE_HOUSE_RENT, RULE_MEDICAL, RULE_TRANSPORT, RULE_FOOD, GET_ATTD_BONUS,
             MIN_ATTD_BONUS, RULE_DEAR_ALW, ATTD_ALW, OT_ALW, NIGHT_BILL, WASHING_BILL, DRIVER_ALW, EXPORT_ALW,
             IS_DEDUCT, RULE_STATUS, RULE_REMARKS)
            VALUES (:ruleId, :ruleName, :ruleBasic, :ruleHouseRent, :ruleMedical, :ruleTransport, :ruleFood,
                    :getAttdBonus, :minAttdBonus, :ruleDearAlw, :attdAlw, :otAlw, :nightBill, :washingBill,
                    :driverAlw, :exportAlw, :isDeduct, :ruleStatus, :ruleRemarks)";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleId", ruleId));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleName", request.RuleName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleBasic", request.RuleBasic));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleHouseRent", request.RuleHouseRent));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleMedical", request.RuleMedical));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleTransport", request.RuleTransport));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleFood", request.RuleFood));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("getAttdBonus", request.GetAttdBonus));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("minAttdBonus", request.MinAttdBonus ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleDearAlw", request.RuleDearAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("attdAlw", request.AttdAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("otAlw", request.OtAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("nightBill", request.NightBill ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("washingBill", request.WashingBill ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("driverAlw", request.DriverAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("exportAlw", request.ExportAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("isDeduct",
            string.IsNullOrWhiteSpace(request.IsDeduct) ? "Y" : request.IsDeduct.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleStatus",
            string.IsNullOrWhiteSpace(request.RuleStatus) ? DBNull.Value : request.RuleStatus.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleRemarks",
            string.IsNullOrWhiteSpace(request.RuleRemarks) ? DBNull.Value : request.RuleRemarks.Trim()));

        await command.ExecuteNonQueryAsync();

        return Results.Ok(new { ok = true, ruleId });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPut("/salary-rules/{id:int}", async (string unit, int id, SalaryRuleRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.RuleName))
    {
        return Results.BadRequest(new { ok = false, message = "Rule name is required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"UPDATE SALARY_RULE_INFO
            SET RULE_NAME = :ruleName,
                RULE_BASIC = :ruleBasic,
                RULE_HOUSE_RENT = :ruleHouseRent,
                RULE_MEDICAL = :ruleMedical,
                RULE_TRANSPORT = :ruleTransport,
                RULE_FOOD = :ruleFood,
                GET_ATTD_BONUS = :getAttdBonus,
                MIN_ATTD_BONUS = :minAttdBonus,
                RULE_DEAR_ALW = :ruleDearAlw,
                ATTD_ALW = :attdAlw,
                OT_ALW = :otAlw,
                NIGHT_BILL = :nightBill,
                WASHING_BILL = :washingBill,
                DRIVER_ALW = :driverAlw,
                EXPORT_ALW = :exportAlw,
                IS_DEDUCT = :isDeduct,
                RULE_STATUS = :ruleStatus,
                RULE_REMARKS = :ruleRemarks,
                UPDATED_DATE = SYSDATE
            WHERE RULE_ID = :ruleId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleName", request.RuleName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleBasic", request.RuleBasic));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleHouseRent", request.RuleHouseRent));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleMedical", request.RuleMedical));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleTransport", request.RuleTransport));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleFood", request.RuleFood));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("getAttdBonus", request.GetAttdBonus));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("minAttdBonus", request.MinAttdBonus ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleDearAlw", request.RuleDearAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("attdAlw", request.AttdAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("otAlw", request.OtAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("nightBill", request.NightBill ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("washingBill", request.WashingBill ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("driverAlw", request.DriverAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("exportAlw", request.ExportAlw ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("isDeduct",
            string.IsNullOrWhiteSpace(request.IsDeduct) ? "Y" : request.IsDeduct.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleStatus",
            string.IsNullOrWhiteSpace(request.RuleStatus) ? DBNull.Value : request.RuleStatus.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleRemarks",
            string.IsNullOrWhiteSpace(request.RuleRemarks) ? DBNull.Value : request.RuleRemarks.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleId", id));

        var updated = await command.ExecuteNonQueryAsync();
        if (updated == 0)
        {
            return Results.NotFound(new { ok = false, message = "Salary rule not found." });
        }

        return Results.Ok(new { ok = true, ruleId = id });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapDelete("/salary-rules/{id:int}", async (string unit, int id, IConfiguration configuration) =>
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
        command.BindByName = true;
        command.CommandText = "DELETE FROM SALARY_RULE_INFO WHERE RULE_ID = :ruleId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("ruleId", id));

        var deleted = await command.ExecuteNonQueryAsync();
        if (deleted == 0)
        {
            return Results.NotFound(new { ok = false, message = "Salary rule not found." });
        }

        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/departments", async (string unit, int? unitId, IConfiguration configuration) =>
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
        command.BindByName = true;
        if (unitId.HasValue)
        {
            command.CommandText = @"SELECT DEPARTMENT_ID, DEPARTMENT_NAME, UNIT_ID, SHORT_NAME, SHOW_PRIORITY, BANG_DEPT_NAME, REMARKS
                FROM DEPARTMENT
                WHERE UNIT_ID = :unitId
                ORDER BY DEPARTMENT_NAME";
            command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("unitId", unitId.Value));
        }
        else
        {
            command.CommandText = @"SELECT DEPARTMENT_ID, DEPARTMENT_NAME, UNIT_ID, SHORT_NAME, SHOW_PRIORITY, BANG_DEPT_NAME, REMARKS
                FROM DEPARTMENT
                ORDER BY DEPARTMENT_NAME";
        }

        await using var reader = await command.ExecuteReaderAsync();
        var items = new List<object>();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                departmentId = reader["DEPARTMENT_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["DEPARTMENT_ID"]),
                departmentName = reader["DEPARTMENT_NAME"]?.ToString(),
                unitId = reader["UNIT_ID"] == DBNull.Value ? 0 : Convert.ToInt32(reader["UNIT_ID"]),
                shortName = reader["SHORT_NAME"]?.ToString(),
                showPriority = reader["SHOW_PRIORITY"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SHOW_PRIORITY"]),
                bangDeptName = reader["BANG_DEPT_NAME"]?.ToString(),
                remarks = reader["REMARKS"]?.ToString()
            });
        }

        return Results.Ok(new { ok = true, items });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/departments", async (string unit, DepartmentRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.DepartmentName) || request.UnitId is null)
    {
        return Results.BadRequest(new { ok = false, message = "Department name and unit are required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        var departmentId = request.DepartmentId;
        if (!departmentId.HasValue || departmentId.Value <= 0)
        {
            await using var idCommand = connection.CreateCommand();
            idCommand.CommandText = "SELECT NVL(MAX(DEPARTMENT_ID), 0) + 1 FROM DEPARTMENT";
            var idResult = await idCommand.ExecuteScalarAsync();
            departmentId = idResult == null || idResult == DBNull.Value ? 1 : Convert.ToInt32(idResult);
        }

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"INSERT INTO DEPARTMENT
            (DEPARTMENT_ID, DEPARTMENT_NAME, UNIT_ID, SHORT_NAME, SHOW_PRIORITY, BANG_DEPT_NAME, REMARKS)
            VALUES (:deptId, :deptName, :unitId, :shortName, :showPriority, :bangDeptName, :remarks)";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("deptId", departmentId.Value));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("deptName", request.DepartmentName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("unitId", request.UnitId.Value));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shortName",
            string.IsNullOrWhiteSpace(request.ShortName) ? DBNull.Value : request.ShortName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("showPriority", request.ShowPriority ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangDeptName",
            string.IsNullOrWhiteSpace(request.BangDeptName) ? DBNull.Value : request.BangDeptName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));

        await command.ExecuteNonQueryAsync();

        return Results.Ok(new { ok = true, departmentId });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPut("/departments/{id:int}", async (string unit, int id, DepartmentRequest request, IConfiguration configuration) =>
{
    var connectionString = GetUnitConnection(unit, configuration);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return MissingUnitResult(unit);
    }

    if (string.IsNullOrWhiteSpace(request.DepartmentName) || request.UnitId is null)
    {
        return Results.BadRequest(new { ok = false, message = "Department name and unit are required." });
    }

    try
    {
        await using var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = @"UPDATE DEPARTMENT
            SET DEPARTMENT_NAME = :deptName,
                UNIT_ID = :unitId,
                SHORT_NAME = :shortName,
                SHOW_PRIORITY = :showPriority,
                BANG_DEPT_NAME = :bangDeptName,
                REMARKS = :remarks,
                UPDATED_DATE = SYSDATE
            WHERE DEPARTMENT_ID = :deptId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("deptName", request.DepartmentName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("unitId", request.UnitId.Value));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("shortName",
            string.IsNullOrWhiteSpace(request.ShortName) ? DBNull.Value : request.ShortName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("showPriority", request.ShowPriority ?? 0));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("bangDeptName",
            string.IsNullOrWhiteSpace(request.BangDeptName) ? DBNull.Value : request.BangDeptName.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("remarks",
            string.IsNullOrWhiteSpace(request.Remarks) ? DBNull.Value : request.Remarks.Trim()));
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("deptId", id));

        var updated = await command.ExecuteNonQueryAsync();
        if (updated == 0)
        {
            return Results.NotFound(new { ok = false, message = "Department not found." });
        }

        return Results.Ok(new { ok = true, departmentId = id });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapDelete("/departments/{id:int}", async (string unit, int id, IConfiguration configuration) =>
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
        command.BindByName = true;
        command.CommandText = "DELETE FROM DEPARTMENT WHERE DEPARTMENT_ID = :deptId";
        command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("deptId", id));

        var deleted = await command.ExecuteNonQueryAsync();
        if (deleted == 0)
        {
            return Results.NotFound(new { ok = false, message = "Department not found." });
        }

        return Results.Ok(new { ok = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, message = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
});

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

static async Task<string> ResolveStatusReasonColumnAsync(Oracle.ManagedDataAccess.Client.OracleConnection connection)
{
    if (await ColumnExistsAsync(connection, "EMP_OFFICIAL", "STS_REASONS"))
    {
        return "E_O.STS_REASONS";
    }

    if (await ColumnExistsAsync(connection, "EMP_OFFICIAL", "REASONS"))
    {
        return "E_O.REASONS";
    }

    return "NULL";
}

static async Task<string> ResolveBangPermanentDistColumnAsync(Oracle.ManagedDataAccess.Client.OracleConnection connection)
{
    return await ColumnExistsAsync(connection, "EMP_PERSONAL", "BANG_PERMANENT_DIST")
        ? "E_P.BANG_PERMANENT_DIST"
        : "NULL";
}

static async Task<string> ResolveBangPermanentPostColumnAsync(Oracle.ManagedDataAccess.Client.OracleConnection connection)
{
    return await ColumnExistsAsync(connection, "EMP_PERSONAL", "BANG_PERMANENT_POST")
        ? "E_P.BANG_PERMANENT_POST"
        : "NULL";
}

static async Task<string> ResolveBangPermanentPsColumnAsync(Oracle.ManagedDataAccess.Client.OracleConnection connection)
{
    return await ColumnExistsAsync(connection, "EMP_PERSONAL", "BANG_PERMANENT_PS")
        ? "E_P.BANG_PERMANENT_PS"
        : "NULL";
}

static async Task<string> ResolveBangPermanentVillColumnAsync(Oracle.ManagedDataAccess.Client.OracleConnection connection)
{
    return await ColumnExistsAsync(connection, "EMP_PERSONAL", "BANG_PERMANENT_VILL")
        ? "E_P.BANG_PERMANENT_VILL"
        : "NULL";
}

static async Task<bool> ColumnExistsAsync(Oracle.ManagedDataAccess.Client.OracleConnection connection, string tableName, string columnName)
{
    await using var command = connection.CreateCommand();
    command.CommandText = @"SELECT COUNT(1)
        FROM USER_TAB_COLUMNS
        WHERE TABLE_NAME = :tableName
          AND COLUMN_NAME = :columnName";
    command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("tableName", tableName.ToUpperInvariant()));
    command.Parameters.Add(new Oracle.ManagedDataAccess.Client.OracleParameter("columnName", columnName.ToUpperInvariant()));

    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt32(result) > 0;
}

app.Run();

record LoginRequest(string Unit, string Username, string Password);
record DepartmentRequest(
    int? DepartmentId,
    string? DepartmentName,
    int? UnitId,
    string? ShortName,
    int? ShowPriority,
    string? BangDeptName,
    string? Remarks);
record ShiftRequest(
    string? ShiftName,
    string? InTime,
    string? OutTime,
    string? InTimeFrom,
    string? OutTimeFrom,
    string? Grace,
    string? GraceOut,
    string? LunchStart,
    string? LunchEnd,
    string? InTimeStart,
    string? OutTimeEnd,
    string? DinerStart,
    string? DinerEnd,
    string? DefaultStatus,
    string? Remarks);
record SectionRequest(
    string? SectionName,
    int? UnitId,
    string? Remarks,
    int? ShowTogether,
    string? BangSecName);
record EmployeeTypeRequest(
    string? CategoryName,
    int? PositionLevel,
    int? TiffinAllowance,
    string? BangTypeName,
    string? Remarks);
record DesignationRequest(
    string? DesignationName,
    string? Grade,
    int? PositionId,
    string? Position,
    int? PositionPriority,
    int? ApprAttdBonus,
    int? OffDayAlw,
    string? BangGrade,
    string? BangDesignationName,
    string? Remarks,
    int? DormEntitle,
    int? NonDormitory);
record SalaryRuleRequest(
    string? RuleName,
    int RuleBasic,
    int RuleHouseRent,
    int RuleMedical,
    int RuleTransport,
    int RuleFood,
    int GetAttdBonus,
    int? MinAttdBonus,
    int? RuleDearAlw,
    int? AttdAlw,
    int? OtAlw,
    int? NightBill,
    int? WashingBill,
    int? DriverAlw,
    int? ExportAlw,
    string? IsDeduct,
    string? RuleStatus,
    string? RuleRemarks);
