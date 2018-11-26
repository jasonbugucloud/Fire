var app = angular.module("ffApp", ['ngMaterial', 'ngMessages', 'ngAnimate', 'moment-picker']);
app.config(['$locationProvider', '$mdDateLocaleProvider', function ($locationProvider, $mdDateLocaleProvider) {
	$locationProvider.html5Mode(true);
	$mdDateLocaleProvider.formatDate = function (date) {
		return moment(date).format('ddd, MM/DD/YYYY');
	}
}]);
app.controller('dashboardController', ['$scope', '$http', '$mdDialog', '$mdToast', function ($scope, $http, $mdDialog, $mdToast) {
	angular.extend($scope, {
		todayDate: new Date(),
		months: ['January', "February", 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', "December"],
		year: new Date().getFullYear()
	});
	$scope.month = $scope.months[new Date().getMonth()];
	var getYearList = function () {
		var ret = [];
		var max = new Date().getFullYear() + 3;
		for (var y = 2017; y <= max; y++) {
			ret.push(y);
		}
		return ret;
	};
	$scope.years = getYearList();

	$scope.FindColor = function (code) {
		var clr;
		$.each($scope.dayCodes, function (i, v) {
			if (v.Code === code) {
				clr = v.Color;
				return false;
			}
		});
		return clr;
	}
	$http.get('Config/GetDayCodeList').then(function (result) {
		$scope.dayCodes = result.data;
		$scope.$watch('todayDate', function (newValue) {
			$scope.theDate = moment(newValue).format('ddd, MM/DD/YYYY');
			$http.get('home/DailyRoster', { params: { date: moment(newValue).format('YYYY-MM-DD HH:mm') } }).then(function (data) {
				$scope.TodayRoster = data.data;
				angular.forEach($scope.TodayRoster, function (v, i) {
					angular.forEach(v.RosterList, function (v2, i2) {
						angular.forEach(v2.Allocations, function (v3, i3) {
							v3.Color = $scope.FindColor(v3.DayCode);
							v3.Range = moment(v3.StartsAt).format('HH:mm') + ' - ' + moment(v3.EndsAt).format('HH:mm');
						});
					});
				});
			});
		});
		$scope.$watchGroup(['month', 'year'], function (newValues, oldValues, scope) {
			$http.get('home/Attendance', { params: { year: scope.year, month: scope.months.indexOf(scope.month) } }).then(function (data) {
				if (data.data) {
					$scope.attendances = data.data.Attendances;
					angular.forEach($scope.attendances, function (v, i) {
						v.Day = moment(v.From).format('ddd, MM/DD/YYYY');
						v.Color = $scope.FindColor(v.DayCode);
						v.Range = moment(v.From).format('HH:mm') + ' - ' + moment(v.To).format('HH:mm');
					});
				}
				else {
					$scope.attendances = null;
				}
			});
		});

	});
	$http.get('home/GetTimeOwingList').then(function (data) {
		$scope.TimeOwingList = data.data;
		angular.forEach($scope.TimeOwingList, function (v, i) {
			v.StartAt = moment(v.StartAt).format('MM/DD/YYYY HH:mm');
			v.EndAt = moment(v.EndAt).format('MM/DD/YYYY HH:mm');
			if (v.ApprovedBy) {
				v.Status = 'Approved';
			}
			else {
				v.Status = 'Pending';
			}
			v.Type = v.Type === 0 ? 'Family Day' : 'Time Owing';
		});
	});
	$http.get('home/GetOverTimeList').then(function (data) {
		$scope.TimeOTList = data.data;
		angular.forEach($scope.TimeOTList, function (v, i) {
			v.StartAt = moment(v.StartAt).format('MM/DD/YYYY HH:mm');
			v.EndAt = moment(v.EndAt).format('MM/DD/YYYY HH:mm');
			if (v.ApprovedBy) {
				v.Status = 'Approved';
			}
			else {
				v.Status = 'Pending';
			}
			v.Reason = v.Reason === 0 ? 'Overtime' : 'Partial Acting Pay';
		});
	});
	$http.get('home/GetNotification').then(function (data) {
		var notifyTo = data.data.notifyTo;
		var notifyOt = data.data.notifyOt;
		if (notifyTo === true || notifyOt === true) {
			$mdDialog.show({
				controller: 'notificationDlgCtrl',
				templateUrl: 'notification.tmpl.html',
				parent: angular.element(document.querySelector('#dashboardContainer')),
				clickOutsideToClose: false,
				fullscreen: true,
				locals: {
					notifyTo: notifyTo,
					notifyOt: notifyOt
				}
			});
		}
	});
	$scope.showRequestOwingDlg = function (e) {
		$mdDialog.show({
			controller: 'reqOwingDlgCtrl',
			templateUrl: 'reqOwing.tmpl.html',
			parent: angular.element(document.querySelector('#dashboardContainer')),
			targetEvent: e,
			clickOutsideToClose: false,
			fullscreen: true,
			locals: {
			}
		}).then(function (request) {
			//add new request in time owing list
			if ($scope.TimeOwingList) {
				$scope.TimeOwingList.unshift({
					Id: request.id,
					StartAt: request.from.format('MM/DD/YYYY HH:mm'),
					EndAt: request.to.format('MM/DD/YYYY HH:mm'),
					Type: request.type == '0' ? 'Family Day' : 'Time Owing',
					Hours: request.to.diff(request.from, 'hours', true).toFixed(2),
					Status: 'Pending'
				});
			}
			$mdToast.show({
				template: '<md-toast class="md-toast successMsg">Time owing requested.</md-toast>',
				position: 'bottom right',
				hideDelay: 2000
			});
		});
	};
	$scope.showRequestOTDlg = function (e) {
		$mdDialog.show({
			controller: 'reqOTDlgCtrl',
			templateUrl: 'reqOT.tmpl.html',
			parent: angular.element(document.querySelector('#dashboardContainer')),
			targetEvent: e,
			clickOutsideToClose: false,
			fullscreen: true,
			locals: {
			}
		}).then(function (request) {
			//add new request in time owing list
			if ($scope.TimeOTList) {
				$scope.TimeOTList.unshift({
					Id: request.id,
					StartAt: request.from.format('MM/DD/YYYY HH:mm'),
					EndAt: request.to.format('MM/DD/YYYY HH:mm'),
					Reason: request.reason == '0' ? 'Overtime' : 'Partial Acting Pay',
					Explanation: request.explanation,
					Hours: request.to.diff(request.from, 'hours', true).toFixed(2),
					Status: 'Pending'
				});
			}
			$mdToast.show({
				template: '<md-toast class="md-toast successMsg">Time owing requested.</md-toast>',
				position: 'bottom right',
				hideDelay: 2000
			});
		});
	};
	$scope.showOTEditDlg = function (e, ot) {
		$mdDialog.show({
			controller: 'editOTDlgCtrl',
			templateUrl: 'reqOT.tmpl.html',
			parent: angular.element(document.querySelector('#dashboardContainer')),
			targetEvent: e,
			clickOutsideToClose: false,
			fullscreen: true,
			locals: {
				id: ot.Id,
				from: moment(ot.StartAt, 'MM/DD/YYYY HH:mm'),
				to: moment(ot.EndAt, 'MM/DD/YYYY HH:mm'),
				reason: ot.Reason === 'Overtime' ? 0 : 1,
				explanation: ot.Explanation
			}
		}).then(function (request) {
			//udpate request in overtime list
			if ($scope.TimeOTList) {
				var index = -1;
				angular.forEach($scope.TimeOTList, function (v, i) {
					if (v.Id === request.id) {
						index = i;
					}
				});
				if (index > -1) {
					$scope.TimeOTList.splice(index, 1, {
						Id: request.id,
						StartAt: request.from.format('MM/DD/YYYY HH:mm'),
						EndAt: request.to.format('MM/DD/YYYY HH:mm'),
						Reason: request.reason == '0' ? 'Overtime' : 'Partial Acting Pay',
						Explanation: request.explanation,
						Hours: request.to.diff(request.from, 'hours', true).toFixed(2),
						Status: 'Pending'
					});
				}
			}
			$mdToast.show({
				template: '<md-toast class="md-toast successMsg">Overtime request updated.</md-toast>',
				position: 'bottom right',
				hideDelay: 2000
			});
		});
	};
	$scope.deleteOT = function (e, ot) {
		$mdDialog.show(
			$mdDialog.confirm()
				.title('Are you sure to delete this Overtime request?')
				.targetEvent(e)
				.ok("Yes")
				.cancel('No')
		).then(function () {
			$http.post('home/DeleteOvertime', { Id: ot.Id }).then(function (result) {
				if (result && result.data) {
					var msg = result.data.message;
					var cls = result.data.success ? 'successMsg' : 'failMsg';
					if (result.data.success) {
						var index = -1;
						angular.forEach($scope.TimeOTList, function (v, i) {
							if (v.Id === ot.Id) {
								index = i;
							}
						});
						if (index > -1) {
							$scope.TimeOTList.splice(index, 1);
						}
					}
					$mdToast.show({
						template: '<md-toast class="md-toast ' + cls + '">' + msg + '</md-toast>',
						position: 'bottom right',
						hideDelay: 2000
					});
				}
			});
		}, function () {
		});
	};
	$scope.showOwingEditDlg = function (e, ot) {
		$mdDialog.show({
			controller: 'editOwingDlgCtrl',
			templateUrl: 'reqOwing.tmpl.html',
			parent: angular.element(document.querySelector('#dashboardContainer')),
			targetEvent: e,
			clickOutsideToClose: false,
			fullscreen: true,
			locals: {
				id: ot.Id,
				from: moment(ot.StartAt, 'MM/DD/YYYY HH:mm'),
				to: moment(ot.EndAt, 'MM/DD/YYYY HH:mm'),
				type: ot.Type === 'Family Day' ? 0 : 1
			}
		}).then(function (request) {
			//update request in time owing list
			if ($scope.TimeOwingList) {
				var index = -1;
				angular.forEach($scope.TimeOwingList, function (v, i) {
					if (v.Id === request.id) {
						index = i;
					}
				});
				if (index > -1) {
					$scope.TimeOwingList.splice(index, 1, {
						Id: request.id,
						StartAt: request.from.format('MM/DD/YYYY HH:mm'),
						EndAt: request.to.format('MM/DD/YYYY HH:mm'),
						Type: request.type == '0' ? 'Family Day' : 'Time Owing',
						Hours: request.to.diff(request.from, 'hours', true).toFixed(2),
						Status: 'Pending'
					});
				}
			}
			$mdToast.show({
				template: '<md-toast class="md-toast successMsg">Time owing request updated.</md-toast>',
				position: 'bottom right',
				hideDelay: 2000
			});
		});
	};
	$scope.deleteOwing = function (e, ot) {
		$mdDialog.show(
			$mdDialog.confirm()
				.title('Are you sure to delete this Time Owing request?')
				.targetEvent(e)
				.ok("Yes")
				.cancel('No')
		).then(function () {
			$http.post('home/DeleteTimeOwing', { Id: ot.Id }).then(function (result) {
				if (result && result.data) {
					var msg = result.data.message;
					var cls = result.data.success ? 'successMsg' : 'failMsg';
					if (result.data.success) {
						var index = -1;
						angular.forEach($scope.TimeOwingList, function (v, i) {
							if (v.Id === ot.Id) {
								index = i;
							}
						});
						if (index > -1) {
							$scope.TimeOwingList.splice(index, 1);
						}
					}
					$mdToast.show({
						template: '<md-toast class="md-toast ' + cls + '">' + msg + '</md-toast>',
						position: 'bottom right',
						hideDelay: 2000
					});
				}
			});
		}, function () {
		});
	};
}
]);
app.controller('reqOwingDlgCtrl', ['$scope', '$mdDialog', 'locals', '$http', function ($scope, $mdDialog, locals, $http) {
	angular.extend($scope, {
		from: moment(),
		to: moment(),
		minScheduleMoment: moment(),
		type: '0',
		error: '',
		ok: function () {
			if ($scope.to <= $scope.from) {
				$scope.error = 'Invalid request.';
				return false;
			}
			$http.post('home/RequestTimeOwing', { StartAt: $scope.from, EndAt: $scope.to, Type: $scope.type }).then(function (result) {
				if (result && result.data) {
					var msg = result.data.message;
					if (result.data.success) {
						$mdDialog.hide({ id: result.data.id, from: $scope.from, to: $scope.to, type: $scope.type });
					}
					else {
						$scope.error = msg;
					}
				}
			});
		},
		cancel: function () {
			$mdDialog.cancel();
		}
	});
}]);
app.controller('editOwingDlgCtrl', ['$scope', '$mdDialog', 'locals', '$http', function ($scope, $mdDialog, locals, $http) {
	angular.extend($scope, {
		id: locals.id,
		from: locals.from,
		to: locals.to,
		minScheduleMoment: moment(),
		type: locals.type,
		error: '',
		ok: function () {
			if ($scope.to <= $scope.from) {
				$scope.error = 'Invalid request.';
				return false;
			}
			$http.post('home/UpdateTimeOwing', { Id: $scope.id, StartAt: $scope.from, EndAt: $scope.to, Type: $scope.type }).then(function (result) {
				if (result && result.data) {
					var msg = result.data.message;
					if (result.data.success) {
						$mdDialog.hide({ id: $scope.id, from: $scope.from, to: $scope.to, type: $scope.type });
					}
					else {
						$scope.error = msg;
					}
				}
			});
		},
		cancel: function () {
			$mdDialog.cancel();
		}
	});
}]);
app.controller('reqOTDlgCtrl', ['$scope', '$mdDialog', 'locals', '$http', function ($scope, $mdDialog, locals, $http) {
	angular.extend($scope, {
		from: moment(),
		to: moment(),
		reason: '0',
		error: '',
		ok: function () {
			if ($scope.to <= $scope.from) {
				$scope.error = 'Invalid request.';
				return false;
			}
			$http.post('home/RequestOvertime', { StartAt: $scope.from, EndAt: $scope.to, Reason: $scope.reason, Explanation: $scope.explanation }).then(function (result) {
				if (result && result.data) {
					var msg = result.data.message;
					if (result.data.success) {
						$mdDialog.hide({ id: result.data.id, from: $scope.from, to: $scope.to, reason: $scope.reason, explanation: $scope.explanation });
					}
					else {
						$scope.error = msg;
					}
				}
			});
		},
		cancel: function () {
			$mdDialog.cancel();
		}
	});
	$scope.$watch('reason', function (newVal) {
		if (newVal == 0) {
			$scope.explanation = '';
		}
	});
}]);
app.controller('editOTDlgCtrl', ['$scope', '$mdDialog', 'locals', '$http', function ($scope, $mdDialog, locals, $http) {
	angular.extend($scope, {
		id: locals.id,
		from: locals.from,
		to: locals.to,
		reason: locals.reason,
		explanation: locals.explanation,
		error: '',
		ok: function () {
			if ($scope.to <= $scope.from) {
				$scope.error = 'Invalid request.';
				return false;
			}
			$http.post('home/UpdateOvertime', { Id: $scope.id, StartAt: $scope.from, EndAt: $scope.to, Reason: $scope.reason, Explanation: $scope.explanation }).then(function (result) {
				if (result && result.data) {
					var msg = result.data.message;
					if (result.data.success) {
						$mdDialog.hide({ id: $scope.id, from: $scope.from, to: $scope.to, reason: $scope.reason, explanation: $scope.explanation });
					}
					else {
						$scope.error = msg;
					}
				}
			});
		},
		cancel: function () {
			$mdDialog.cancel();
		}
	});
	$scope.$watch('reason', function (newVal) {
		if (newVal == 0) {
			$scope.explanation = '';
		}
	});
}]);
app.controller('notificationDlgCtrl', ['$scope', '$mdDialog', 'locals', '$http', function ($scope, $mdDialog, locals, $http) {
	angular.extend($scope, {
		notifyTo: locals.notifyTo,
		notifyOt: locals.notifyOt,
		ok: function () {
			$http.post('home/SetNotified', {});
			$mdDialog.hide();
		}
	});
}]);
function convertJsonToDate(json) {
	if (json) {
		var date_num = parseInt(json.match(/-?\d+/)[0]);
		if (date_num > 0) {
			return new Date(date_num);
		}
	}
	return null;
}
function signOut(name) {
	if (window.document.documentMode) {
		document.execCommand('ClearAuthenticationCache');
		window.location = 'Base/SignOut';
	}
	var n = window.location.href;
	$.ajax({
		type: 'GET',
		url: n,
		username: name,
		password: 'reset',
		statusCode: {
			401: function () {
				window.location = 'Base/SignOut';
			}
		}
	});
}