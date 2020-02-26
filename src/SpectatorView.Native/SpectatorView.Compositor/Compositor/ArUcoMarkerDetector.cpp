#include "stdafx.h"

#include "ArUcoMarkerDetector.h"

ArUcoMarkerDetector::ArUcoMarkerDetector() {}

ArUcoMarkerDetector::~ArUcoMarkerDetector() {}

template <class T>
void OutputDebugMatrix(const std::wstring& prompt, const cv::Mat& mat)
{
    auto output = prompt;
    for (int m = 0; m < mat.rows; m++)
    {
        for (int n = 0; n < mat.cols; n++)
        {
            auto value = mat.at<T>(m, n);
            output += (L", " + std::to_wstring(value));
        }
    }

    OutputDebugString(output.data());
}

bool ArUcoMarkerDetector::DetectMarkers(
    unsigned char* imageData,
    int imageWidth,
    int imageHeight,
    float* focalLength,
    float* principalPoint,
    float* radialDistortion,
    float* tangentialDistortion,
    float markerSize,
    int arUcoMarkerDictionaryId)
{
    // This dll assumes that its handed pixels in BGRA format
    cv::Mat image(imageHeight, imageWidth, CV_8UC4, imageData);

    // ArUco detection with opencv does not support images with alpha channels
    // So, we convert the image to grayscale for processing
    cv::Mat grayImage;
    cv::cvtColor(image, grayImage, cv::ColorConversionCodes::COLOR_BGRA2GRAY);

    std::vector<int> arUcoMarkerIds;
    std::vector<std::vector<cv::Point2f>> arUcoMarkers;
    std::vector<std::vector<cv::Point2f>> arUcoRejectedCandidates;
    auto arUcoDetectorParameters = cv::aruco::DetectorParameters::create();
    auto arUcoDictionary = cv::aruco::getPredefinedDictionary(cv::aruco::PREDEFINED_DICTIONARY_NAME(arUcoMarkerDictionaryId));

    // Detect markers
    cv::aruco::detectMarkers(
        grayImage,
        arUcoDictionary,
        arUcoMarkers,
        arUcoMarkerIds,
        arUcoDetectorParameters,
        arUcoRejectedCandidates);

    auto logText = L"Completed marker detection: " + std::to_wstring(arUcoMarkerIds.size()) + L" ids found";
    OutputDebugString(logText.data());

    // Note: there are some assumed memory sizes for the provided float pointers.
    // focalLength - float[] with 2 elements
    // principalPoint - float[] with 2 elements
    // radialDistortion - float[] with 3 elements
    // tangentialDistortion - float[] with 2 elements
    cv::Mat cameraMatrix(3, 3, CV_64F, cv::Scalar(0));
    cameraMatrix.at<double>(0, 0) = focalLength[0]; // X focal length
    cameraMatrix.at<double>(0, 2) = principalPoint[0]; // X principal point
    cameraMatrix.at<double>(1, 1) = focalLength[1]; // Y focal length
    cameraMatrix.at<double>(1, 2) = principalPoint[1]; // Y principal point
    cameraMatrix.at<double>(2, 2) = 1.0; // Default value for camera intrinsic matrix
    OutputDebugMatrix<double>(L"Camera Matrix: ", cameraMatrix);

    cv::Mat distCoeffMatrix(1, 5, CV_64F, cv::Scalar(0));
    distCoeffMatrix.at<double>(0, 0) = radialDistortion[0]; // r1 radial distortion
    distCoeffMatrix.at<double>(0, 1) = radialDistortion[1]; // r2 radial distortion
    distCoeffMatrix.at<double>(0, 2) = tangentialDistortion[0]; // t1 tangential distortion
    distCoeffMatrix.at<double>(0, 3) = tangentialDistortion[1]; // t2 tangential distortion
    distCoeffMatrix.at<double>(0, 4) = radialDistortion[2]; // r3 radial distortion
    OutputDebugMatrix<double>(L"Distortion Coefficients: ", distCoeffMatrix);

    std::vector<cv::Vec3d> rotationVecs;
    std::vector<cv::Vec3d> translationVecs;
    cv::aruco::estimatePoseSingleMarkers(
        arUcoMarkers,
        markerSize,
        cameraMatrix,
        distCoeffMatrix,
        rotationVecs,
        translationVecs);

    _detectedMarkers.clear();
    for (size_t i = 0; i < arUcoMarkerIds.size(); i++)
    {
        auto id = arUcoMarkerIds[i];

        auto posText = L"OpenCV Marker Position: " + std::to_wstring(translationVecs[i][0]) + L", " + std::to_wstring(translationVecs[i][1]) + L", " + std::to_wstring(translationVecs[i][2]);
        OutputDebugString(posText.data());

        auto rotText = L"OpenCV Marker Rotation: " + std::to_wstring(rotationVecs[i][0]) + L", " + std::to_wstring(rotationVecs[i][1]) + L", " + std::to_wstring(rotationVecs[i][2]);
        OutputDebugString(rotText.data());

        Marker marker;
        marker.id = id;
        marker.position.x = static_cast<float>(translationVecs[i][0]);
        marker.position.y = static_cast<float>(translationVecs[i][1]);
        marker.position.z = static_cast<float>(translationVecs[i][2]);
        marker.rotation.x = static_cast<float>(rotationVecs[i][0]);
        marker.rotation.y = static_cast<float>(rotationVecs[i][1]);
        marker.rotation.z = static_cast<float>(rotationVecs[i][2]);
        _detectedMarkers[id] = marker;
    }

    return true;
}

bool ArUcoMarkerDetector::GetDetectedMarkerIds(int* _detectedIds, int size)
{
    if (_detectedMarkers.size() > static_cast<size_t>(size))
    {
        return false;
    }

    int index = 0;
    for (auto markerPair : _detectedMarkers)
    {
        markerPair.second.id;
        _detectedIds[index] = markerPair.second.id;
        index++;
    }

    return true;
}

bool ArUcoMarkerDetector::GetDetectedMarkerPose(int _detectedId, Vector3* position, Vector3* rotation)
{
    if (_detectedMarkers.find(_detectedId) == _detectedMarkers.end())
    {
        return false;
    }

    auto marker = _detectedMarkers.at(_detectedId);

    memcpy_s(position, sizeof(*position), &marker.position, sizeof(marker.position));
    memcpy_s(rotation, sizeof(*rotation), &marker.rotation, sizeof(marker.rotation));

    return true;
}

uint16_t* ArUcoMarkerDetector::GetDilatedMask(uint16_t* mask, int col, int row)
{
    int dilation_type = cv::MORPH_RECT;
    int dilation_size = 1;

    cv::Size size(col, row);

    cv::Mat nonDilatedMask = cv::Mat(size, CV_16U, mask);

    cv::Mat dilatedMask = cv::Mat(size, CV_16U);

    cv::Mat kernal = cv::getStructuringElement(dilation_type,
        cv::Size(2 * dilation_size + 1, 2 * dilation_size + 1),
        cv::Point(dilation_size, dilation_size));

    cv::dilate(nonDilatedMask, dilatedMask, kernal);

    return mask;
}