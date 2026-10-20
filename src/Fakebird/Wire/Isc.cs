namespace Fakebird.Wire;

/// <summary>Firebird wire protocol constants (subset used by the managed .NET client).</summary>
internal static class Op
{
    public const int Connect = 1;
    public const int Exit = 2;
    public const int Accept = 3;
    public const int Reject = 4;
    public const int Disconnect = 6;
    public const int Response = 9;
    public const int Attach = 19;
    public const int Create = 20;
    public const int Detach = 21;
    public const int Transaction = 29;
    public const int Commit = 30;
    public const int Rollback = 31;
    public const int Prepare = 32;
    public const int CreateBlob = 34;
    public const int OpenBlob = 35;
    public const int GetSegment = 36;
    public const int PutSegment = 37;
    public const int CancelBlob = 38;
    public const int CloseBlob = 39;
    public const int InfoDatabase = 40;
    public const int InfoTransaction = 42;
    public const int InfoBlob = 43;
    public const int BatchSegments = 44;
    public const int CommitRetaining = 50;
    public const int Prepare2 = 51;
    public const int OpenBlob2 = 56;
    public const int CreateBlob2 = 57;
    public const int SeekBlob = 61;
    public const int AllocateStatement = 62;
    public const int Execute = 63;
    public const int ExecImmediate = 64;
    public const int Fetch = 65;
    public const int FetchResponse = 66;
    public const int FreeStatement = 67;
    public const int PrepareStatement = 68;
    public const int SetCursor = 69;
    public const int InfoSql = 70;
    public const int Dummy = 71;
    public const int Execute2 = 76;
    public const int SqlResponse = 78;
    public const int DropDatabase = 81;
    public const int RollbackRetaining = 86;
    public const int Cancel = 91;
    public const int Ping = 93;
}

internal static class Isc
{
    public const int ProtocolFlag = 0x8000;
    public const int Protocol10 = 10;
    public const int Protocol11 = ProtocolFlag | 11;
    public const int Protocol12 = ProtocolFlag | 12;
    public const int PtypeLazySend = 5;
    public const int ArchGeneric = 1;

    public const int DSQL_close = 1;
    public const int DSQL_drop = 2;
    public const int DSQL_unprepare = 4;

    // info
    public const byte info_end = 1;
    public const byte info_truncated = 2;
    public const byte info_error = 3;
    public const byte info_firebird_version = 103;
    public const byte info_db_id = 4;
    public const byte info_implementation = 11;
    public const byte info_isc_version = 12;
    public const byte info_page_size = 14;
    public const byte info_ods_version = 32;
    public const byte info_ods_minor_version = 33;
    public const byte info_db_sql_dialect = 62;
    public const byte info_db_read_only = 63;
    public const byte info_db_class = 102;
    public const byte info_attachment_id = 22;
    public const byte info_server_version = 12;

    public const byte info_sql_select = 4;
    public const byte info_sql_bind = 5;
    public const byte info_sql_num_variables = 6;
    public const byte info_sql_describe_vars = 7;
    public const byte info_sql_describe_end = 8;
    public const byte info_sql_sqlda_seq = 9;
    public const byte info_sql_message_seq = 10;
    public const byte info_sql_type = 11;
    public const byte info_sql_sub_type = 12;
    public const byte info_sql_scale = 13;
    public const byte info_sql_length = 14;
    public const byte info_sql_null_ind = 15;
    public const byte info_sql_field = 16;
    public const byte info_sql_relation = 17;
    public const byte info_sql_owner = 18;
    public const byte info_sql_alias = 19;
    public const byte info_sql_sqlda_start = 20;
    public const byte info_sql_stmt_type = 21;
    public const byte info_sql_get_plan = 22;
    public const byte info_sql_records = 23;
    public const byte info_sql_batch_fetch = 24;
    public const byte info_sql_relation_alias = 25;
    public const byte info_sql_explain_plan = 26;
    public const byte info_sql_stmt_flags = 27;

    public const byte info_req_select_count = 13;
    public const byte info_req_insert_count = 14;
    public const byte info_req_update_count = 15;
    public const byte info_req_delete_count = 16;

    public const byte info_tra_id = 4;
    public const byte info_tra_isolation = 8;
    public const byte info_tra_access = 9;

    public const byte info_blob_num_segments = 4;
    public const byte info_blob_max_segment = 5;
    public const byte info_blob_total_length = 6;
    public const byte info_blob_type = 7;

    // statement types
    public const int stmt_select = 1;
    public const int stmt_insert = 2;
    public const int stmt_update = 3;
    public const int stmt_delete = 4;
    public const int stmt_ddl = 5;
    public const int stmt_exec_procedure = 8;
    public const int stmt_start_trans = 9;
    public const int stmt_commit = 10;
    public const int stmt_rollback = 11;
    public const int stmt_select_for_upd = 12;
    public const int stmt_set_generator = 13;
    public const int stmt_savepoint = 14;

    // dpb
    public const int dpb_user_name = 28;
    public const int dpb_password = 29;
    public const int dpb_lc_ctype = 48;
    public const int dpb_sql_dialect = 63;
    public const int dpb_sql_role_name = 60;

    // tpb
    public const int tpb_consistency = 1;
    public const int tpb_concurrency = 2;
    public const int tpb_wait = 6;
    public const int tpb_nowait = 7;
    public const int tpb_read = 8;
    public const int tpb_write = 9;
    public const int tpb_lock_read = 10;
    public const int tpb_lock_write = 11;
    public const int tpb_read_committed = 15;
    public const int tpb_rec_version = 17;
    public const int tpb_no_rec_version = 18;
    public const int tpb_lock_timeout = 21;

    // status vector
    public const int arg_end = 0;
    public const int arg_gds = 1;
    public const int arg_string = 2;
    public const int arg_number = 4;
    public const int arg_interpreted = 5;
    public const int arg_warning = 18;
    public const int arg_sql_state = 19;

    // sql types
    public const int SQL_TEXT = 452;
    public const int SQL_VARYING = 448;
    public const int SQL_SHORT = 500;
    public const int SQL_LONG = 496;
    public const int SQL_FLOAT = 482;
    public const int SQL_DOUBLE = 480;
    public const int SQL_D_FLOAT = 530;
    public const int SQL_TIMESTAMP = 510;
    public const int SQL_BLOB = 520;
    public const int SQL_ARRAY = 540;
    public const int SQL_QUAD = 550;
    public const int SQL_TYPE_TIME = 560;
    public const int SQL_TYPE_DATE = 570;
    public const int SQL_INT64 = 580;
    public const int SQL_INT128 = 32752;
    public const int SQL_TIMESTAMP_TZ = 32754;
    public const int SQL_TIME_TZ = 32756;
    public const int SQL_BOOLEAN = 32764;
    public const int SQL_NULL = 32766;

    // blr
    public const int blr_text = 14;
    public const int blr_text2 = 15;
    public const int blr_short = 7;
    public const int blr_long = 8;
    public const int blr_quad = 9;
    public const int blr_int64 = 16;
    public const int blr_float = 10;
    public const int blr_double = 27;
    public const int blr_d_float = 11;
    public const int blr_timestamp = 35;
    public const int blr_varying = 37;
    public const int blr_varying2 = 38;
    public const int blr_cstring = 40;
    public const int blr_blob2 = 17;
    public const int blr_sql_date = 12;
    public const int blr_sql_time = 13;
    public const int blr_bool = 23;
    public const int blr_int128 = 26;
    public const int blr_version4 = 4;
    public const int blr_version5 = 5;
    public const int blr_begin = 2;
    public const int blr_message = 4;
    public const int blr_end = 255;
    public const int blr_eoc = 76;

    // charsets
    public const int CS_NONE = 0;
    public const int CS_OCTETS = 1;
    public const int CS_UTF8 = 4;
}
