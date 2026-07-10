using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ResetYourFuture.Application.DTOs;

namespace ResetYourFuture.Web.OpenApi;

/// <summary>
/// Registers the built-in ASP.NET Core OpenAPI document ("v1") together with the
/// project's document/operation transformers (API metadata, JWT bearer security
/// scheme, and conditional per-operation security requirements).
/// </summary>
internal static class OpenApiExtensions
{
    public static IServiceCollection AddResetYourFutureOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            // Top-level document info + the reusable "Bearer" security scheme.
            options.AddDocumentTransformer<ApiInfoAndSecuritySchemeTransformer>();
            // Attaches the bearer requirement only to operations that actually require auth.
            options.AddOperationTransformer<BearerSecurityRequirementTransformer>();
            // Fills in parameter descriptions and clearer response descriptions across all operations.
            options.AddOperationTransformer<ParameterAndResponseDocsTransformer>();
            // Attaches request-body examples to the request DTOs (drives Swagger UI "Try it out" prefill).
            options.AddSchemaTransformer<RequestExampleSchemaTransformer>();
        });

        return services;
    }
}

/// <summary>
/// Document transformer: sets the OpenAPI <c>info</c> block (title, version, description,
/// contact, license) and registers the reusable JWT <c>Bearer</c> security scheme under
/// <c>components.securitySchemes</c> when JWT bearer authentication is configured.
/// </summary>
internal sealed class ApiInfoAndSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        // --- API metadata (info block) ---
        document.Info = new OpenApiInfo
        {
            Title = "ResetYourFuture API",
            Version = "v1",
            Description =
                "REST API for the ResetYourFuture learning platform: authentication, courses, " +
                "lessons, assessments, certificates, subscriptions, profiles, blog, testimonials, " +
                "chat, media, and administration. Secured endpoints use JWT bearer tokens issued " +
                "by POST /api/auth/login.\n\n" +
                "## Realtime chat (SignalR)\n\n" +
                "The chat hub is **not** part of this OpenAPI document — OpenAPI describes stateless " +
                "HTTP request/response operations only, whereas SignalR is a bidirectional WebSocket " +
                "protocol. It is documented here for reference; the REST `Chat` endpoints below cover " +
                "history and load-on-demand, while the hub handles real-time delivery.\n\n" +
                "**Hub URL:** `/hubs/chat` (WebSockets). **Auth:** pass the JWT as a query-string token — " +
                "`/hubs/chat?access_token=<token>`. Available to every authenticated user.\n\n" +
                "**Methods you invoke (client → server):**\n" +
                "- `SendMessage(conversationId: guid, content: string)` — `content` is capped at 4,000 characters.\n" +
                "- `MarkAsRead(conversationId: guid)` — marks the other party's messages as read.\n\n" +
                "**Events you handle (server → client):**\n" +
                "- `ReceiveMessage(message: ChatMessageDto)` — delivered to both participants.\n" +
                "- `ChatNotification(notification: ChatNotificationDto)` — delivered to the recipient.\n" +
                "- `ChatError(message: string)` — e.g. over the character limit.\n\n" +
                "The `ChatMessageDto` and `ChatNotificationDto` payload shapes are listed under **Schemas** below.\n\n" +
                "**Connect example:**\n" +
                "```js\n" +
                "const conn = new signalR.HubConnectionBuilder()\n" +
                "  .withUrl('/hubs/chat', { accessTokenFactory: () => jwt })\n" +
                "  .build();\n" +
                "conn.on('ReceiveMessage', m => console.log(m));\n" +
                "await conn.start();\n" +
                "await conn.invoke('SendMessage', conversationId, 'Hello');\n" +
                "```\n\n" +
                "## AI Assistant (Server-Sent Events)\n\n" +
                "`POST /api/assistant/chat` is a normal HTTP endpoint, but its response is a `text/event-stream` " +
                "body rather than a single JSON payload — Swagger UI's \"Try it out\" will show the raw stream, " +
                "not a parsed object. Each SSE `data:` line is a JSON-encoded `AssistantStreamEvent` whose `Kind` " +
                "is `token` (one piece of the reply), `sources` (grounding citations, sent once after the last " +
                "token), `done` (stream complete), or `error`. The endpoint is entirely local (an Ollama sidecar, " +
                "no cloud calls) and runs against every authenticated user regardless of subscription tier.",
            Contact = new OpenApiContact
            {
                Name = "ResetYourFuture Support",
                Email = "support@resetyourfuture.local"
            },
            License = new OpenApiLicense
            {
                Name = "Proprietary — © ResetYourFuture"
            }
        };

        // --- JWT bearer security scheme (drives the Swagger UI "Authorize" button) ---
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (schemes.Any(s => s.Name == "Bearer"))
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT",
                Description =
                    "JWT Authorization header using the Bearer scheme. Obtain a token from " +
                    "POST /api/auth/login and paste only the token value (the 'Bearer ' prefix is added automatically)."
            };
        }

        // --- Realtime payload schemas ---
        // ChatNotificationDto is used only by the SignalR hub and ChatMessageDto may be inlined,
        // so neither is guaranteed to reach components/schemas via the REST endpoints. Register
        // them explicitly so the "Realtime chat (SignalR)" section above can reference their shapes.
        document.Components ??= new OpenApiComponents();
        foreach (var (name, type) in new (string Name, Type Type)[]
        {
            ( "ChatMessageDto" , typeof( ChatMessageDto ) ) ,
            ( "ChatNotificationDto" , typeof( ChatNotificationDto ) ) ,
        })
        {
            if (document.Components.Schemas?.ContainsKey(name) == true)
                continue;

            var schema = await context.GetOrCreateSchemaAsync(type, null, cancellationToken);
            document.AddComponent(name, schema);
        }
    }
}

/// <summary>
/// Operation transformer: adds the <c>Bearer</c> security requirement and a 401 response to
/// every operation that requires authorization, while skipping operations explicitly marked
/// <see cref="AllowAnonymousAttribute"/> or with no authorization metadata. This keeps the
/// padlock icon accurate per endpoint instead of marking the whole document as secured.
/// </summary>
internal sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Responses ??= new OpenApiResponses();

        // Every operation can surface an unhandled server error (ProblemDetails in production).
        operation.Responses.TryAdd("500", new OpenApiResponse
        {
            Description = "Server error — an unexpected error occurred."
        });

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();

        if (allowsAnonymous || !requiresAuthorization)
            return Task.CompletedTask;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Unauthorized — a valid JWT bearer token is required."
        });
        operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = "Forbidden — the authenticated user lacks the required role or permission."
        });

        return Task.CompletedTask;
    }
}

/// <summary>
/// Operation transformer: fills in a description for every parameter (curated text for well-known
/// route/query parameters, humanized fallback otherwise) and replaces ASP.NET Core's generic
/// inferred response descriptions (e.g. "OK", "Bad Request") with clearer wording. Descriptions
/// already set by other transformers (401/403/500) or by XML comments are left untouched.
/// </summary>
internal sealed class ParameterAndResponseDocsTransformer : IOpenApiOperationTransformer
{
    private static readonly Dictionary<string, string> ParameterDocs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = "Unique identifier (GUID) of the resource.",
        ["courseId"] = "Course identifier (GUID).",
        ["moduleId"] = "Module identifier (GUID).",
        ["lessonId"] = "Lesson identifier (GUID).",
        ["certificateId"] = "Certificate identifier (GUID).",
        ["conversationId"] = "Conversation identifier (GUID).",
        ["verificationId"] = "Public certificate verification identifier (GUID).",
        ["assessmentId"] = "Assessment definition identifier (GUID).",
        ["userId"] = "User identifier.",
        ["roleName"] = "Role name (e.g. 'Admin' or 'Student').",
        ["page"] = "1-based page number. Defaults to 1.",
        ["pageSize"] = "Number of items per page (1–100). Defaults to 10.",
        ["lang"] = "Language code: 'en' or 'el'. Defaults to 'en'.",
        ["search"] = "Optional case-insensitive search term.",
        ["query"] = "Search term matched against email or name.",
        ["count"] = "Maximum number of items to return.",
        ["slug"] = "URL slug of the article.",
        ["type"] = "Asset type: 'pdf' or 'video'.",
        ["filePath"] = "Relative path of the public media file.",
        ["sortBy"] = "Field to sort by (e.g. 'email').",
        ["sortDir"] = "Sort direction: 'asc' or 'desc'.",
    };

    private static readonly Dictionary<string, string> ResponseDocs = new()
    {
        ["200"] = "Success.",
        ["201"] = "Created.",
        ["204"] = "Success — no content returned.",
        ["400"] = "Validation failed — see the error payload for details.",
        ["404"] = "The requested resource was not found.",
        ["409"] = "Conflict — the resource already exists or violates a uniqueness constraint.",
    };

    // ASP.NET Core's default inferred descriptions that we consider "generic" and safe to replace.
    private static readonly HashSet<string> GenericDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "" , "OK" , "Created" , "No Content" , "Bad Request" , "Not Found" , "Conflict" ,
    };

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(parameter.Description))
                    continue;

                var name = parameter.Name ?? string.Empty;
                parameter.Description = ParameterDocs.TryGetValue(name, out var doc)
                    ? doc
                    : Humanize(name);
            }
        }

        if (operation.Responses is not null)
        {
            foreach (var (code, response) in operation.Responses)
            {
                if (!string.IsNullOrWhiteSpace(response.Description) &&
                     !GenericDescriptions.Contains(response.Description))
                    continue;

                if (ResponseDocs.TryGetValue(code, out var doc))
                    response.Description = doc;
            }
        }

        return Task.CompletedTask;
    }

    // "pageSize" -> "Page size.", "moduleId" -> "Module id." — a readable fallback for any
    // parameter not in the curated dictionary.
    private static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        sb.Append(char.ToUpperInvariant(name[0]));
        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c)) sb.Append(' ').Append(char.ToLowerInvariant(c));
            else sb.Append(c);
        }
        sb.Append('.');
        return sb.ToString();
    }
}

/// <summary>
/// Schema transformer: attaches a representative <c>example</c> to each request-body DTO so the
/// Swagger UI "Try it out" panel is pre-filled with valid, ready-to-send sample payloads.
/// </summary>
internal sealed class RequestExampleSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly IReadOnlyDictionary<Type, Func<JsonNode>> Examples = new Dictionary<Type, Func<JsonNode>>
    {
        [typeof(RefreshTokenRequestDto)] = () => new JsonObject
        {
            ["refreshToken"] = "9f8c1e2b7a6d4f3e0b5c8a1d2e3f4a5b6c7d8e9f0a1b2c3d"
        },
        [typeof(ForgotPasswordRequestDto)] = () => new JsonObject
        {
            ["email"] = "student@example.com"
        },
        [typeof(ResetPasswordRequestDto)] = () => new JsonObject
        {
            ["email"] = "student@example.com",
            ["token"] = "CfDJ8N…password-reset-token",
            ["newPassword"] = "P@ssw0rd123",
            ["confirmPassword"] = "P@ssw0rd123"
        },
        [typeof(DevResetPasswordRequestDto)] = () => new JsonObject
        {
            ["email"] = "student@example.com",
            ["newPassword"] = "P@ssw0rd123"
        },
        [typeof(AdminSetPasswordDto)] = () => new JsonObject
        {
            ["newPassword"] = "P@ssw0rd123"
        },
        [typeof(UpdateProfileRequest)] = () => new JsonObject
        {
            ["firstName"] = "Maria",
            ["lastName"] = "Papadopoulou",
            ["displayName"] = "Maria P.",
            ["dateOfBirth"] = "1998-04-12"
        },
        [typeof(ChangePasswordRequest)] = () => new JsonObject
        {
            ["currentPassword"] = "OldP@ssw0rd1",
            ["newPassword"] = "NewP@ssw0rd2"
        },
        [typeof(StartConversationRequest)] = () => new JsonObject
        {
            ["targetUserId"] = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            ["initialMessage"] = "Hi, I have a question about the resilience course."
        },
        [typeof(CreateCheckoutRequest)] = () => new JsonObject
        {
            ["planId"] = "3f2504e0-4f89-11d3-9a0c-0305e82c3301"
        },
        [typeof(SubmitAssessmentRequest)] = () => new JsonObject
        {
            ["answersJson"] = "{\"q1\":\"sometimes\",\"q2\":4}",
            ["summaryJson"] = "{\"score\":72,\"band\":\"moderate\"}"
        },
        [typeof(SaveAssessmentDefinitionRequest)] = () => new JsonObject
        {
            ["key"] = "burnout-check",
            ["titleEn"] = "Burnout Self-Check",
            ["titleEl"] = "Αυτοέλεγχος Εξουθένωσης",
            ["descriptionEn"] = "A short self-assessment of burnout risk.",
            ["descriptionEl"] = "Σύντομη αυτοαξιολόγηση κινδύνου εξουθένωσης.",
            ["schemaJson"] = "{\"questions\":[{\"key\":\"q1\",\"labelEn\":\"How often do you feel exhausted?\"}]}"
        },
        [typeof(SaveCourseRequest)] = () => new JsonObject
        {
            ["titleEn"] = "Foundations of Resilience",
            ["titleEl"] = "Θεμέλια Ανθεκτικότητας",
            ["descriptionEn"] = "An introductory course on building personal resilience.",
            ["descriptionEl"] = "Εισαγωγικό μάθημα για την ανθεκτικότητα.",
            ["requiredTier"] = 0
        },
        [typeof(SaveModuleRequest)] = () => new JsonObject
        {
            ["titleEn"] = "Week 1 — Getting Started",
            ["titleEl"] = "Εβδομάδα 1 — Ξεκινώντας",
            ["descriptionEn"] = "Orientation and goals.",
            ["descriptionEl"] = "Προσανατολισμός και στόχοι.",
            ["sortOrder"] = 1,
            ["courseId"] = "3f2504e0-4f89-11d3-9a0c-0305e82c3301"
        },
        [typeof(SaveLessonRequest)] = () => new JsonObject
        {
            ["titleEn"] = "What is resilience?",
            ["titleEl"] = "Τι είναι η ανθεκτικότητα;",
            ["contentEn"] = "<p>Resilience is the capacity to recover from difficulties.</p>",
            ["contentEl"] = "<p>Η ανθεκτικότητα είναι η ικανότητα ανάκαμψης.</p>",
            ["videoUrl"] = "https://videos.example.com/lesson-1.mp4",
            ["durationMinutes"] = 12,
            ["sortOrder"] = 1,
            ["moduleId"] = "5a7b9c1d-2e3f-4a5b-6c7d-8e9f0a1b2c3d"
        },
        [typeof(SaveBlogArticleRequest)] = () => new JsonObject
        {
            ["titleEn"] = "Five habits that build resilience",
            ["titleEl"] = "Πέντε συνήθειες ανθεκτικότητας",
            ["slug"] = "five-habits-that-build-resilience",
            ["summaryEn"] = "Small daily habits that compound into lasting resilience.",
            ["summaryEl"] = "Μικρές καθημερινές συνήθειες.",
            ["contentEn"] = "<p>Full article body…</p>",
            ["contentEl"] = "<p>Πλήρες κείμενο…</p>",
            ["coverImageUrl"] = "blog/covers/resilience.jpg",
            ["authorName"] = "Dr. A. Mentor",
            ["tags"] = new JsonArray("mindset", "habits"),
            ["isPublished"] = false
        },
        [typeof(SaveTestimonialRequest)] = () => new JsonObject
        {
            ["fullName"] = "Alex Doe",
            ["roleOrTitle"] = "Career Changer",
            ["companyOrContext"] = "2025 Cohort",
            ["quoteText"] = "This program completely reset my career direction.",
            ["displayOrder"] = 1,
            ["isActive"] = true
        },
    };

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Example is null &&
             Examples.TryGetValue(context.JsonTypeInfo.Type, out var factory))
        {
            schema.Example = factory();
        }

        return Task.CompletedTask;
    }
}
