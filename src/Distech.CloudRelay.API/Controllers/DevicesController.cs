﻿using Distech.CloudRelay.API.Model;
using Distech.CloudRelay.API.Services;
using Distech.CloudRelay.Common.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Distech.CloudRelay.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class DevicesController : ControllerBase
    {
        #region Constants

        // 200 MB limit to support large file upload
        // the same limit is also defined in web.config for IIS hosting
        private const int RequestPayloadSizeLimit = 200 * 1024 * 1024;

        #endregion

        #region Members

        private readonly IDeviceService m_DeviceService;
        private readonly IFileService m_FileService;
        private readonly ITelemetryService m_TelemetryService;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="deviceService"></param>
        /// <param name="fileService"></param>
        /// <param name="telemetryService"></param>
        public DevicesController(IDeviceService deviceService, IFileService fileService, ITelemetryService telemetryService)
        {
            m_DeviceService = deviceService;
            m_FileService = fileService;
            m_TelemetryService = telemetryService;
        }

        #endregion

        #region GET

        /// <summary>
        /// Sends a request to the specified device.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="remoteQuery"></param>
        /// <returns></returns>
        [HttpGet("{deviceId:minlength(1)}/request")]
        public async Task<IActionResult> GetDeviceRequestAsync(string deviceId, [Required, FromHeader(Name = DeviceRequest.RemoteQueryHeaderName)] string remoteQuery)
        {
            return await this.SendDeviceRequestAsync(deviceId);
        }

        #endregion

        #region PUT

        /// <summary>
        /// Sends a request to the specified device.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="remoteQuery"></param>
        /// <returns></returns>
        [HttpPut("{deviceId:minlength(1)}/request")]
        public async Task<IActionResult> PutDeviceRequestAsync(string deviceId, [Required, FromHeader(Name = DeviceRequest.RemoteQueryHeaderName)] string remoteQuery)
        {
            return await this.SendDeviceRequestAsync(deviceId);
        }

        #endregion

        #region POST

        /// <summary>
        /// Sends a request to the specified device.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="remoteQuery"></param>
        /// <returns></returns>
        [RequestSizeLimit(RequestPayloadSizeLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = RequestPayloadSizeLimit, BufferBodyLengthLimit = RequestPayloadSizeLimit)]
        [HttpPost("{deviceId:minlength(1)}/request")]
        public async Task<IActionResult> PostDeviceRequestAsync(string deviceId, [Required, FromHeader(Name = DeviceRequest.RemoteQueryHeaderName)] string remoteQuery)
        {
            return await this.SendDeviceRequestAsync(deviceId);
        }

        #endregion

        #region DELETE

        /// <summary>
        /// Sends a request to the specified device.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="remoteQuery"></param>
        /// <returns></returns>
        [HttpDelete("{deviceId:minlength(1)}/request")]
        public async Task<IActionResult> DeleteDeviceRequestAsync(string deviceId, [Required, FromHeader(Name = DeviceRequest.RemoteQueryHeaderName)] string remoteQuery)
        {
            return await this.SendDeviceRequestAsync(deviceId);
        }

        #endregion

        #region Send Device Request

        /// <summary>
        /// Sends a request to the specified device, wait for its response and return the result.
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        [NonAction]
        private async Task<IActionResult> SendDeviceRequestAsync(string deviceId)
        {
            IActionResult result = null;

            await m_TelemetryService.IncrementCounterAsync("senddevicerequest", new string[] { });

            var request = await m_TelemetryService.StartTimerAsync(() => m_DeviceService.CreateRequestAsync(deviceId, this.Request), "create_request", new string[] {  });

            var response = await m_TelemetryService.StartTimerAsync(() => m_DeviceService.InvokeRequestAsync(deviceId, request), "invoke_request", new string[] {  });

                
            switch (response)
            {
                case DeviceInlineResponse inlineResponse:
                    result = new ContentResult()
                    {
                        Content = inlineResponse.Body,
                        StatusCode = response.Headers.Status,
                        ContentType = response.Headers.ContentType
                    };
                    break;

                case DeviceFileResponse fileResponse:
                    result = File(await m_TelemetryService.StartTimerAsync(() => m_FileService.OpenFileAsync(deviceId, fileResponse.BlobUrl), "blob_storage.read_file", new string[] { }), response.Headers.ContentType);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported response type: {response}");
            }

            // set headers that are not handled by the IActionResult implementation
            this.Response.SetHeadersFromDeviceResponse(response.Headers);
            m_TelemetryService.Dispose();
            return result;
        }

        #endregion
    }
}
