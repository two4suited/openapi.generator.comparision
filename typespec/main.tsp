import "@typespec/http";
import "@typespec/openapi3";

using TypeSpec.Http;

@service(#{ title: "Task API" })
@server("http://localhost:5000", "Local")
namespace TaskApi;

enum TaskStatus {
  Todo,
  InProgress,
  Done,
}

/** A task. */
model TaskItem {
  id: int32;
  title: string;
  status: TaskStatus;
  dueDate?: plainDate;

  /** Name of the client that created the task (from the X-Client header). */
  createdBy?: string;
}

/** Payload for creating a task. */
model CreateTask {
  @minLength(1) @maxLength(100) title: string;
  dueDate?: plainDate;
}

@route("/tasks")
@tag("Tasks")
interface Tasks {
  /** List tasks, optionally filtered by status */
  @get listTasks(@query status?: TaskStatus): TaskItem[];

  @get getTask(@path id: int32): TaskItem | NotFoundResponse;

  @post createTask(
    @body body: CreateTask,
    /** Identifies the calling client. */ @header("X-Client") client?: string,
  ): {
    @statusCode _: 201;
    @header location: string;
    @body body: TaskItem;
  };

  @delete deleteTask(@path id: int32): NoContentResponse | NotFoundResponse;
}
