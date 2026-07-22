using Microsoft.AspNetCore.Mvc;

namespace Kosync.Controllers;

[ApiController]
public class ManagementController(ILogger<ManagementController> logger,
                                  ProxyService proxyService,
                                  IPService ipService,
                                  KosyncDb db,
                                  UserService userService) : ControllerBase
{
    [HttpDelete("manage/progress")]
    public ObjectResult DeleteProgress(string username)
    {
        if (!userService.IsAuthenticated || !userService.IsAdmin)
        {
            return StatusCode(407, new { });
        }
        var userCollection = db.Context.GetCollection<User>("users");

        var user = userCollection.FindOne(u => u.Username == username);


        if (user is not null)
        {
            user.Documents.Clear();

            userCollection.Update(user);
        }

        return StatusCode(201, new { });
    }

    [HttpGet("/manage/users")]
    public ObjectResult GetUsers()
    {
        if (!userService.IsAuthenticated)
        {
            if (string.IsNullOrEmpty(userService.Username))
            {
                LogWarning("Unauthenticated GET request to /manage/users.");
            }
            else
            {
                LogWarning($"Unauthenticated GET request to /manage/users with username [{userService.Username}].");
            }

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsAdmin)
        {
            LogWarning($"Unauthorized GET request to /manage/users from user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsActive)
        {
            LogWarning($"GET request to /manage/users received from inactive user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }


        var userCollection = db.Context.GetCollection<User>("users");

        var users = userCollection.FindAll().Select(i => new
        {
            id = i.Id,
            username = i.Username,
            isAdministrator = i.IsAdministrator,
            isActive = i.IsActive,
            documentCount = i.Documents.Count()
        });

        LogInfo($"User [{userService.Username}] requested /manage/users");
        return StatusCode(200, users);
    }

    [HttpPost("/manage/users")]
    public ObjectResult CreateUser(UserCreateRequest payload)
    {
        if (!userService.IsAuthenticated)
        {
            if (string.IsNullOrEmpty(userService.Username))
            {
                LogWarning("Unauthenticated POST request to /manage/users.");
            }
            else
            {
                LogWarning($"Unauthenticated POST request to /manage/users with username [{userService.Username}].");
            }

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsAdmin)
        {
            LogWarning($"Unauthorized POST request to /manage/users from user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsActive)
        {
            LogWarning($"POST request to /manage/users received from inactive user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        var userCollection = db.Context.GetCollection<User>("users");

        var existingUser = userCollection.FindOne(i => i.Username == payload.username);
        if (existingUser is not null)
        {
            return StatusCode(400, new
            {
                message = "User already exists"
            });
        }

        var passwordHash = Utility.HashPassword(payload.password);

        var user = new User()
        {
            Username = payload.username,
            PasswordHash = passwordHash,
            IsAdministrator = false
        };

        userCollection.Insert(user);
        userCollection.EnsureIndex(u => u.Username);

        LogInfo($"User [{payload.username}] created by user [{userService.Username}]");
        return StatusCode(200, new
        {
            message = "User created successfully"
        });
    }

    [HttpDelete("/manage/users")]
    public ObjectResult DeleteUser(string username)
    {
        if (!userService.IsAuthenticated)
        {
            if (string.IsNullOrEmpty(userService.Username))
            {
                LogWarning("Unauthenticated DELETE request to /manage/users.");
            }
            else
            {
                LogWarning($"Unauthenticated DELETE request to /manage/users with username [{userService.Username}].");
            }

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }


        if (!userService.IsAdmin &&
            !username.Equals(userService.Username, StringComparison.OrdinalIgnoreCase))
        // allow a user to delete their own account
        {
            LogWarning($"Unauthorized DELETE request to /manage/users from user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsActive)
        {
            LogWarning($"DELETE request to /manage/users from inactive user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        var userCollection = db.Context.GetCollection<User>("users");

        var user = userCollection.FindOne(u => u.Username == username);

        if (user is null)
        {
            LogInfo($"DELETE request to /manage/users received from [{userService.Username}] but target username [{username}] does not exist.");

            return StatusCode(404, new
            {
                message = "User does not exist"
            });
        }

        userCollection.Delete(user.Id);

        LogInfo($"User [{username}] has been deleted by [{userService.Username}]");

        return StatusCode(200, new
        {
            message = "Success"
        });
    }

    [HttpGet("/manage/users/documents")]
    public ObjectResult GetDocuments(string username)
    {
        if (!userService.IsAuthenticated)
        {
            if (string.IsNullOrEmpty(userService.Username))
            {
                LogWarning("Unauthenticated GET request to /manage/users/documents.");
            }
            else
            {
                LogWarning($"Unauthenticated GET request to /manage/users/documents with username [{userService.Username}].");
            }

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }


        if (!userService.IsAdmin &&
            !username.Equals(userService.Username, StringComparison.OrdinalIgnoreCase))
        // allow a user to request their own docs
        {
            LogWarning($"Unauthorized GET request to /manage/users/documents from user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsActive)
        {
            LogWarning($"GET request to /manage/users/documents received from inactive user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        LogInfo($"User [{username}]'s documents requested by [{userService.Username}]");

        var userCollection = db.Context.GetCollection<User>("users");

        var user = userCollection.FindOne(i => i.Username == username);
        if (user is null)
        {
            return StatusCode(400, new
            {
                message = "User does not exist"
            });
        }

        return StatusCode(200, user.Documents);
    }

    [HttpDelete("/manage/users/documents")]
    public ObjectResult DeleteUserDocument(string username, string documentHash)
    {
        if (!userService.IsAuthenticated)
        {
            if (string.IsNullOrEmpty(userService.Username))
            {
                LogWarning("Unauthenticated DELETE request to /manage/users/documents.");
            }
            else
            {
                LogWarning($"Unauthenticated DELETE request to /manage/users/documents with username [{userService.Username}].");
            }

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsAdmin &&
            !username.Equals(userService.Username, StringComparison.OrdinalIgnoreCase))
        {
            LogWarning($"Unauthorized DELETE request to /manage/users/documents from user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsActive)
        {
            LogWarning($"DELETE request to /manage/users/documents from inactive user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        var userCollection = db.Context.GetCollection<User>("users").Include(i => i.Documents);

        var user = userCollection.FindOne(i => i.Username == username);

        var document = user.Documents.SingleOrDefault(i => i.DocumentHash == documentHash);

        if (document is null)
        {
            return StatusCode(404, new
            {
                message = $"Document hash [{documentHash}] was not found for user [{username}]."
            });
        }

        user.Documents.Remove(document);

        userCollection.Update(user);

        LogInfo($"User [{userService.Username}] deleted document with hash [{documentHash}] for user [{username}].");

        return StatusCode(200, new
        {
            message = "Success"
        });
    }

    [HttpPut("/manage/users/active")]
    public ObjectResult UpdateUserActive(string username)
    {
        if (!userService.IsAuthenticated)
        {
            if (string.IsNullOrEmpty(userService.Username))
            {
                LogWarning("Unauthenticated PUT request to /manage/users/active.");
            }
            else
            {
                LogWarning($"Unauthenticated PUT request to /manage/users/active with username [{userService.Username}].");
            }

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsAdmin)
        {
            LogWarning($"Unauthorized PUT request to /manage/users/active from user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsActive)
        {
            LogWarning($"PUT request to /manage/users/active received from inactive user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (username == "admin")
        {
            LogWarning($"Attempt to toggle admin user active from user [{userService.Username}].");

            return StatusCode(400, new
            {
                message = "Cannot update admin user"
            });
        }

        var userCollection = db.Context.GetCollection<User>("users");

        var user = userCollection.FindOne(i => i.Username == username);
        if (user is null)
        {
            LogInfo($"PUT request to /manage/users/active received from [{userService.Username}] but target username [{username}] does not exist.");

            return StatusCode(400, new
            {
                message = "User does not exist"
            });
        }

        user.IsActive = !user.IsActive;
        userCollection.Update(user);

        LogInfo($"User [{username}] set to {(user.IsActive ? "active" : "inactive")} by user [{userService.Username}]");

        return StatusCode(200, new
        {
            message = user.IsActive ? "User marked as active" : "User marked as inactive"
        });
    }

    [HttpPut("/manage/users/password")]
    public ObjectResult UpdatePassword(string username, PasswordChangeRequest payload)
    {
        if (!userService.IsAuthenticated)
        {
            if (string.IsNullOrEmpty(userService.Username))
            {
                LogWarning("Unauthenticated PUT request to /manage/users/password.");
            }
            else
            {
                LogWarning($"Unauthenticated PUT request to /manage/users/password with username [{userService.Username}].");
            }

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsAdmin)
        {
            LogWarning($"Unauthorized PUT request to /manage/users/password from user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        if (!userService.IsActive)
        {
            LogWarning($"PUT request to /manage/users/password received from inactive user [{userService.Username}].");

            return StatusCode(401, new
            {
                message = "Unauthorized"
            });
        }

        // KOReader will literally not attempt to log in with a blank password field or with just whitespace
        if (string.IsNullOrWhiteSpace(payload.password))
        {
            return StatusCode(400, new
            {
                message = "Password cannot be empty or whitespace"
            });
        }

        if (username == "admin")
        {
            LogWarning($"Attempt to change admin password from user [{userService.Username}].");
            return StatusCode(400, new
            {
                message = "Cannot update admin user"
            });
        }

        var userCollection = db.Context.GetCollection<User>("users");

        var user = userCollection.FindOne(i => i.Username == username);
        if (user is null)
        {
            LogWarning($"Password change request received from [{userService.Username}] but target username [{username}] does not exist.");
            return StatusCode(400, new
            {
                message = "User does not exist"
            });
        }

        user.PasswordHash = Utility.HashPassword(payload.password);
        userCollection.Update(user);

        LogInfo($"User [{username}]'s password updated by [{userService.Username}].");
        return StatusCode(200, new
        {
            message = "Password changed successfully"
        });
    }

    private void LogInfo(string text)
    {
        Log(LogLevel.Information, text);
    }

    private void LogWarning(string text)
    {
        Log(LogLevel.Warning, text);
    }

    private void Log(LogLevel level, string text)
    {
        string logMsg = $"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] [{ipService.ClientIP}]";

        // If trusted proxies are set but this request comes from another address, mark it
        if (proxyService.TrustedProxies.Length > 0 &&
            !ipService.TrustedProxy)
        {
            logMsg += "*";
        }

        logMsg += $" {text}";

        logger?.Log(level, logMsg);
    }
}