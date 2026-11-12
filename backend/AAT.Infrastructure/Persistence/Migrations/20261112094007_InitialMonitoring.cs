using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    runtime = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_models", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "environments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_environments", x => x.id);
                    table.ForeignKey(
                        name: "fk_environments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    health_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    last_seen_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    credential_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agents", x => x.id);
                    table.ForeignKey(
                        name: "fk_agents_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enrollment_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    used_by_agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollment_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_enrollment_tokens_environments_environment_id",
                        column: x => x.environment_id,
                        principalTable: "environments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollment_tokens_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_heartbeats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    agent_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    health_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_heartbeats", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_heartbeats_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    integration_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_applications", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_applications_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hardware_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sampled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cpu_percent = table.Column<double>(type: "double precision", nullable: false),
                    memory_used_bytes = table.Column<long>(type: "bigint", nullable: false),
                    memory_total_bytes = table.Column<long>(type: "bigint", nullable: false),
                    gpu_utilization_percent = table.Column<double>(type: "double precision", nullable: true),
                    gpu_memory_used_bytes = table.Column<long>(type: "bigint", nullable: true),
                    gpu_memory_total_bytes = table.Column<long>(type: "bigint", nullable: true),
                    gpu_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    collection_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hardware_metrics", x => x.id);
                    table.ForeignKey(
                        name: "fk_hardware_metrics_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "execution_traces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    time_to_first_token_ms = table.Column<int>(type: "integer", nullable: true),
                    total_latency_ms = table.Column<int>(type: "integer", nullable: false),
                    tokens_per_second = table.Column<double>(type: "double precision", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_execution_traces", x => x.id);
                    table.ForeignKey(
                        name: "fk_execution_traces_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_execution_traces_ai_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "ai_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_execution_traces_models_model_id",
                        column: x => x.model_id,
                        principalTable: "models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_execution_traces_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_heartbeats_agent_id_reported_at_utc",
                table: "agent_heartbeats",
                columns: new[] { "agent_id", "reported_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_agents_credential_hash",
                table: "agents",
                column: "credential_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agents_environment_id",
                table: "agents",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "ix_agents_organization_id_last_seen_utc",
                table: "agents",
                columns: new[] { "organization_id", "last_seen_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_applications_agent_id",
                table: "ai_applications",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_organization_id_timestamp_utc",
                table: "audit_logs",
                columns: new[] { "organization_id", "timestamp_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollment_tokens_environment_id",
                table: "enrollment_tokens",
                column: "environment_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollment_tokens_organization_id",
                table: "enrollment_tokens",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollment_tokens_token_hash",
                table: "enrollment_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_environments_organization_id_name",
                table: "environments",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_execution_traces_agent_id",
                table: "execution_traces",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_execution_traces_application_id",
                table: "execution_traces",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_execution_traces_model_id",
                table: "execution_traces",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_execution_traces_organization_id_agent_id_started_at_utc",
                table: "execution_traces",
                columns: new[] { "organization_id", "agent_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_execution_traces_organization_id_event_id",
                table: "execution_traces",
                columns: new[] { "organization_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_execution_traces_organization_id_started_at_utc",
                table: "execution_traces",
                columns: new[] { "organization_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_execution_traces_request_id",
                table: "execution_traces",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_metrics_agent_id",
                table: "hardware_metrics",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_hardware_metrics_organization_id_agent_id_sampled_at_utc",
                table: "hardware_metrics",
                columns: new[] { "organization_id", "agent_id", "sampled_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_hardware_metrics_organization_id_event_id",
                table: "hardware_metrics",
                columns: new[] { "organization_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_models_organization_id_provider_model_name",
                table: "models",
                columns: new[] { "organization_id", "provider", "model_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_organization_id",
                table: "users",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_heartbeats");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "enrollment_tokens");

            migrationBuilder.DropTable(
                name: "execution_traces");

            migrationBuilder.DropTable(
                name: "hardware_metrics");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "ai_applications");

            migrationBuilder.DropTable(
                name: "models");

            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "environments");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
