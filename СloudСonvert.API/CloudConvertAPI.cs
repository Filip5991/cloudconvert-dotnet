using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using СloudСonvert.API.Models.JobModels;
using СloudСonvert.API.Models.TaskModels;

namespace СloudСonvert.API
{
  public interface ICloudConvertAPI
  {
    #region Jobs
    Task<JobsResponse> GetAllJobsAsync(JobFilter jobFilter);
    Task<JobResponse> CreateJobAsync(JobCreateRequest request);
    Task<JobResponse> GetJobAsync(string id);
    Task<JobResponse> WaitJobAsync(string id);
    Task DeleteJobAsync(string id);
    #endregion

    #region Tasks
    Task<TasksResponse> GetAllTasksAsync(TaskFilter jobFilter);
    Task<TaskResponse> CreateTaskAsync<T>(string operation, T request);
    Task<TaskResponse> GetTaskAsync(string id, string[] include = null);
    Task<TaskResponse> WaitTaskAsync(string id);
    Task DeleteTaskAsync(string id);
    #endregion

    Task<string> UploadAsync(string url, byte[] file, string fileName, object parameters = null);
    bool ValidateWebhookSignatures(string payloadString, string signature, string signingSecret);
  }

  public class CloudConvertAPI : ICloudConvertAPI
  {
    readonly string _apiUrl;
    readonly RestHelper _restHelper;
    readonly string _api_key = "Bearer ";
    const string sandboxUrlApi = "https://api.sandbox.cloudconvert.com/v2";
    const string publicUrlApi = "https://api.cloudconvert.com/v2";

    public CloudConvertAPI(string api_key, bool isSandbox = false)
    {
      _apiUrl = isSandbox ? sandboxUrlApi : publicUrlApi;
      _api_key += api_key;
      _restHelper = new RestHelper();
    }

    public CloudConvertAPI(string url, string api_key)
    {
      _apiUrl = url;
      _api_key += api_key;
      _restHelper = new RestHelper();
    }

    private HttpRequestMessage GetRequest(string endpoint, HttpMethod method, object model = null)
    {
      var request = new HttpRequestMessage { RequestUri = new Uri(endpoint), Method = method };
      
      if (model != null)
      {
        var content = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
        request.Content = content;
      }
      
      request.Headers.Add("Authorization", _api_key);
     
      return request;
    }

    private HttpRequestMessage GetMultipartFormDataRequest(string endpoint, HttpMethod method, byte[] file, string fileName, Dictionary<string, string> parameters = null)
    {
      var content = new MultipartFormDataContent();
      var request = new HttpRequestMessage { RequestUri = new Uri(endpoint), Method = method, };

      content.Add(new ByteArrayContent(file), "file", fileName);

      if (parameters != null)
      {
        foreach (var param in parameters)
        {
          content.Add(new StringContent(param.Value), param.Key);
        }
      }

      request.Content = content;

      return request;
    }

    #region Jobs

    /// <summary>
    /// List all jobs. Requires the task.read scope.
    /// </summary>
    /// <param name="jobFilter"></param>
    /// <returns>
    /// The list of jobs. You can find details about the job model response in the documentation about the show jobs endpoint.
    /// </returns>
    public Task<JobsResponse> GetAllJobsAsync(JobFilter jobFilter) => _restHelper.RequestAsync<JobsResponse>(GetRequest($"{_apiUrl}/jobs?filter[status]={jobFilter.Status}&filter[tag]={jobFilter.Tag}&include={jobFilter.Include}&per_page={jobFilter.PerPage}&page={jobFilter.Page}", HttpMethod.Get));

    /// <summary>
    /// Create a job with one ore more tasks. Requires the task.write scope.
    /// </summary>
    /// <param name="model"></param>
    /// <returns>
    /// The created job. You can find details about the job model response in the documentation about the show jobs endpoint.
    /// </returns>
    public Task<JobResponse> CreateJobAsync(JobCreateRequest model) => _restHelper.RequestAsync<JobResponse>(GetRequest($"{_apiUrl}/jobs", HttpMethod.Post, model));

    /// <summary>
    /// Show a job. Requires the task.read scope.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task<JobResponse> GetJobAsync(string id) => _restHelper.RequestAsync<JobResponse>(GetRequest($"{_apiUrl}/jobs/{id}", HttpMethod.Get));

    /// <summary>
    /// Wait until the job status is finished or error. This makes the request block until the job has been completed. Requires the task.read scope.
    /// 
    /// We do not recommend using this for long running jobs (e.g. video encodings). 
    /// Your system might automatically time out requests if there is not data transferred for a longer time.
    /// In general, please avoid to block your application until a CloudConvert job completes.
    /// There might be cases in which we need to queue your job which results in longer processing times than usual.
    /// Using an asynchronous approach with webhooks is beneficial in such cases.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>
    /// The finished or failed job, including tasks. You can find details about the job model response in the documentation about the show job endpoint.
    /// </returns>
    public Task<JobResponse> WaitJobAsync(string id) => _restHelper.RequestAsync<JobResponse>(GetRequest($"{_apiUrl}/jobs/{id}/wait", HttpMethod.Get));

    /// <summary>
    /// Delete a job, including all tasks and data. Requires the task.write scope.
    /// Jobs are deleted automatically 24 hours after they have ended.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>
    /// An empty response with HTTP Code 204.
    /// </returns>
    public Task DeleteJobAsync(string id) => _restHelper.RequestAsync<object>(GetRequest($"{_apiUrl}/jobs/{id}", HttpMethod.Delete));

    #endregion

    #region Tasks

    /// <summary>
    /// List all tasks with their status, payload and result. Requires the task.read scope.
    /// </summary>
    /// <param name="taskFilter"></param>
    /// <returns>
    /// The list of tasks. You can find details about the task model response in the documentation about the show tasks endpoint.
    /// </returns>
    public Task<TasksResponse> GetAllTasksAsync(TaskFilter taskFilter) => _restHelper.RequestAsync<TasksResponse>(GetRequest($"{_apiUrl}/tasks?filter[job_id]={taskFilter.JobId}&filter[status]={taskFilter.Status}&filter[operation]={taskFilter.Operation}&include={taskFilter.Include}&per_page={taskFilter.PerPage}&page={taskFilter.Page}", HttpMethod.Get));

    /// <summary>
    /// Create task.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="model"></param>
    /// <returns>
    /// The created task. You can find details about the task model response in the documentation about the show tasks endpoint.
    /// </returns>
    public Task<TaskResponse> CreateTaskAsync<T>(string operation, T model) => _restHelper.RequestAsync<TaskResponse>(GetRequest($"{_apiUrl}/{operation}", HttpMethod.Post, model));

    /// <summary>
    /// Show a task. Requires the task.read scope.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="include"></param>
    /// <returns></returns>
    public Task<TaskResponse> GetTaskAsync(string id, string[] include = null) => _restHelper.RequestAsync<TaskResponse>(GetRequest($"{_apiUrl}/tasks/{id}?include={include}", HttpMethod.Get));

    /// <summary>
    /// Wait until the task status is finished or error. This makes the request block until the task has been completed. Requires the task.read scope.
    /// 
    /// We do not recommend using this for long running jobs (e.g. video encodings). 
    /// Your system might automatically time out requests if there is not data transferred for a longer time.
    /// In general, please avoid to block your application until a CloudConvert job completes.
    /// There might be cases in which we need to queue your task which results in longer processing times than usual.
    /// Using an asynchronous approach with webhooks is beneficial in such cases.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>
    /// The finished or failed task. You can find details about the task model response in the documentation about the show tasks endpoint.
    /// </returns>
    public Task<TaskResponse> WaitTaskAsync(string id) => _restHelper.RequestAsync<TaskResponse>(GetRequest($"{_apiUrl}/tasks/{id}/wait", HttpMethod.Get));

    /// <summary>
    /// Delete a task, including all data. Requires the task.write scope.
    /// Tasks are deleted automatically 24 hours after they have ended.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>
    /// An empty response with HTTP Code 204.
    /// </returns>
    public Task DeleteTaskAsync(string id) => _restHelper.RequestAsync<object>(GetRequest($"{_apiUrl}/tasks/{id}", HttpMethod.Delete));

    #endregion

    public Task<string> UploadAsync(string url, byte[] file, string fileName, object parameters = null) => _restHelper.RequestAsync(GetMultipartFormDataRequest($"{url}", HttpMethod.Post, file, fileName, GetParameters(parameters, fileName)));

    public bool ValidateWebhookSignatures(string payloadString, string signature, string signingSecret)
    {
      string hashHMAC = HashHMAC(signingSecret, payloadString);

      return hashHMAC == signature;
    }

    private string HashHMAC(string key, string message)
    {
      byte[] hash = new HMACSHA256(Encoding.UTF8.GetBytes(key)).ComputeHash(new ASCIIEncoding().GetBytes(message));
      return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private Dictionary<string, string> GetParameters(object parameters, string fileName)
    {
      var attributes = ((JToken)parameters).ToList();
      Dictionary<string, string> dictionaryParameters = new Dictionary<string, string>();
      foreach (JToken attribute in attributes)
      {
        JProperty jProperty = attribute.ToObject<JProperty>();
        dictionaryParameters.Add(jProperty.Name, jProperty.Value.ToString().Replace("${filename}", fileName));
      }

      return dictionaryParameters;
    }
  }
}
